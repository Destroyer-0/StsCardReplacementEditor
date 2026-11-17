using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace CardArtTool;

public static class ImageOps
{
    public static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif" };

    // 5 位/通道共 32768 格：够定位色群，又不至于把噪点放大成色块
    private const int Bins = 1 << 15;
    private const int MaxPaletteColors = 256;

    /// <summary>读图后立即释放文件句柄，避免后续覆盖失败。</summary>
    public static Image? LoadNoLock(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var raw = Image.FromStream(stream);
            return new Bitmap(raw);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>统一转成 32 位 PNG 落盘（保留透明通道）。</summary>
    public static void SaveAsPng(string sourcePath, byte[] sourceBytes, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        // 已经就是目标文件时先落地到临时文件，否则原地覆盖会握住句柄
        var samePath = string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(targetPath),
            StringComparison.OrdinalIgnoreCase);
        if (samePath)
        {
            File.Copy(targetPath, targetPath + ".tmp", overwrite: true);
            sourcePath = targetPath + ".tmp";
        }

        try
        {
            using var stream = new MemoryStream(sourceBytes);
            using var raw = Image.FromStream(stream);
            using var bitmap = new Bitmap(raw.Width, raw.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.DrawImage(raw, 0, 0, raw.Width, raw.Height);
            }

            QuantizeToPalette(bitmap);
            bitmap.Save(targetPath, ImageFormat.Png);
        }
        finally
        {
            if (samePath) File.Delete(targetPath + ".tmp");
        }
    }

    /// <summary>中位切分 + Floyd–Steinberg 抖动压到 256 色。JPG 源的压缩噪点会撑出几十万种颜色，抹掉它们约省 2/3 体积。</summary>
    private static void QuantizeToPalette(Bitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            var stride = data.Stride;
            var pixels = new byte[stride * height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

            var counts = new long[Bins];
            var sumR = new long[Bins];
            var sumG = new long[Bins];
            var sumB = new long[Bins];
            for (var y = 0; y < height; y++)
            {
                var row = y * stride;
                for (var x = 0; x < width; x++)
                {
                    var i = row + x * 4;
                    int r = pixels[i + 2], g = pixels[i + 1], b = pixels[i];
                    var bin = (r >> 3) << 10 | (g >> 3) << 5 | (b >> 3);
                    counts[bin]++;
                    sumR[bin] += r;
                    sumG[bin] += g;
                    sumB[bin] += b;
                }
            }

            var used = new List<int>();
            for (var i = 0; i < Bins; i++)
            {
                if (counts[i] > 0) used.Add(i);
            }

            // 本来就没超调色板上限，量化只会白添噪点
            if (used.Count <= MaxPaletteColors) return;

            var boxes = new List<ColorBox> { new() { Bins = used } };
            Measure(boxes[0], counts);

            while (boxes.Count < MaxPaletteColors)
            {
                var target = -1;
                long bestScore = 0;
                for (var i = 0; i < boxes.Count; i++)
                {
                    var box = boxes[i];
                    if (box.Bins.Count < 2) continue;
                    var range = Math.Max(box.MaxR - box.MinR, Math.Max(box.MaxG - box.MinG, box.MaxB - box.MinB));
                    if (range <= 0) continue;
                    // 优先切「色差大且像素多」的盒子，人眼最敏感的区域分得最细
                    var score = (long)range * box.Weight;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        target = i;
                    }
                }
                if (target < 0) break;

                var split = boxes[target];
                var axis = ChooseAxis(split);
                split.Bins.Sort((a, b) => GetAxis(a, axis).CompareTo(GetAxis(b, axis)));

                long half = split.Weight / 2, acc = 0;
                var cut = 1;
                for (var i = 0; i < split.Bins.Count - 1; i++)
                {
                    acc += counts[split.Bins[i]];
                    cut = i + 1;
                    if (acc >= half) break;
                }

                var left = new ColorBox();
                var right = new ColorBox();
                for (var i = 0; i < split.Bins.Count; i++)
                {
                    (i < cut ? left.Bins : right.Bins).Add(split.Bins[i]);
                }
                Measure(left, counts);
                Measure(right, counts);
                boxes[target] = left;
                boxes.Add(right);
            }

            var paletteSize = boxes.Count;
            var paletteR = new byte[paletteSize];
            var paletteG = new byte[paletteSize];
            var paletteB = new byte[paletteSize];
            for (var i = 0; i < paletteSize; i++)
            {
                long weight = 0, r = 0, g = 0, b = 0;
                foreach (var bin in boxes[i].Bins)
                {
                    weight += counts[bin];
                    r += sumR[bin];
                    g += sumG[bin];
                    b += sumB[bin];
                }
                if (weight == 0) weight = 1;
                paletteR[i] = (byte)(r / weight);
                paletteG[i] = (byte)(g / weight);
                paletteB[i] = (byte)(b / weight);
            }

            // 同色格的最近色恒定，缓存一次省掉 O(像素×256) 的重复搜索
            var cache = new int[Bins];
            Array.Fill(cache, -1);
            for (var y = 0; y < height; y++)
            {
                var row = y * stride;
                for (var x = 0; x < width; x++)
                {
                    var i = row + x * 4;
                    int r = pixels[i + 2], g = pixels[i + 1], b = pixels[i];
                    var bin = (r >> 3) << 10 | (g >> 3) << 5 | (b >> 3);
                    var index = cache[bin];
                    if (index < 0)
                    {
                        index = Nearest(paletteR, paletteG, paletteB, r, g, b);
                        cache[bin] = index;
                    }

                    int nr = paletteR[index], ng = paletteG[index], nb = paletteB[index];

                    // 误差向右侧和下一行摊开，否则渐变会出现色带；alpha 不动
                    int er = r - nr, eg = g - ng, eb = b - nb;
                    Scatter(pixels, stride, width, height, x + 1, y, er, eg, eb, 7);
                    Scatter(pixels, stride, width, height, x - 1, y + 1, er, eg, eb, 3);
                    Scatter(pixels, stride, width, height, x, y + 1, er, eg, eb, 5);
                    Scatter(pixels, stride, width, height, x + 1, y + 1, er, eg, eb, 1);

                    pixels[i + 2] = (byte)nr;
                    pixels[i + 1] = (byte)ng;
                    pixels[i] = (byte)nb;
                }
            }

            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private sealed class ColorBox
    {
        public List<int> Bins = new();
        public long Weight;
        public int MinR, MaxR, MinG, MaxG, MinB, MaxB;
    }

    private static void Measure(ColorBox box, long[] counts)
    {
        int minR = 31, maxR = 0, minG = 31, maxG = 0, minB = 31, maxB = 0;
        long weight = 0;
        foreach (var bin in box.Bins)
        {
            weight += counts[bin];
            int r = bin >> 10, g = bin >> 5 & 31, b = bin & 31;
            if (r < minR) minR = r;
            if (r > maxR) maxR = r;
            if (g < minG) minG = g;
            if (g > maxG) maxG = g;
            if (b < minB) minB = b;
            if (b > maxB) maxB = b;
        }
        box.Weight = weight;
        box.MinR = minR;
        box.MaxR = maxR;
        box.MinG = minG;
        box.MaxG = maxG;
        box.MinB = minB;
        box.MaxB = maxB;
    }

    private static int ChooseAxis(ColorBox box)
    {
        int r = box.MaxR - box.MinR, g = box.MaxG - box.MinG, b = box.MaxB - box.MinB;
        if (g >= r && g >= b) return 1;
        return b >= r && b >= g ? 2 : 0;
    }

    private static int GetAxis(int bin, int axis) => axis switch
    {
        0 => bin >> 10,
        1 => bin >> 5 & 31,
        _ => bin & 31,
    };

    private static int Nearest(byte[] paletteR, byte[] paletteG, byte[] paletteB, int r, int g, int b)
    {
        var index = 0;
        long best = long.MaxValue;
        for (var i = 0; i < paletteR.Length; i++)
        {
            int dr = r - paletteR[i], dg = g - paletteG[i], db = b - paletteB[i];
            // 绿色权重最高，近似人眼亮度敏感度
            var distance = (long)dr * dr * 3 + (long)dg * dg * 6 + (long)db * db;
            if (distance >= best) continue;
            best = distance;
            index = i;
        }
        return index;
    }

    private static void Scatter(byte[] pixels, int stride, int width, int height, int x, int y, int er, int eg, int eb, int share)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        var i = y * stride + x * 4;
        pixels[i + 2] = Clamp(pixels[i + 2] + er * share / 16);
        pixels[i + 1] = Clamp(pixels[i + 1] + eg * share / 16);
        pixels[i] = Clamp(pixels[i] + eb * share / 16);
    }

    private static byte Clamp(int value) => (byte)(value < 0 ? 0 : value > 255 ? 255 : value);
}
