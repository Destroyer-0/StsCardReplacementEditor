using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace CardArtTool;

public static class ImageOps
{
    public static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif" };

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

            bitmap.Save(targetPath, ImageFormat.Png);
        }
        finally
        {
            if (samePath) File.Delete(targetPath + ".tmp");
        }
    }
}
