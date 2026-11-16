using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CardArtTool;

// 用户可能选到上层目录（images、或仓库根），这里向下探查到真正可用的位置
public static class PathResolver
{
    private const int MaxDepth = 4;

    public static string ResolveOriginalRoot(string input)
    {
        var path = Clean(input);
        if (!Directory.Exists(path)) return path;
        if (string.Equals(Path.GetFileName(path), "card_portraits", StringComparison.OrdinalIgnoreCase)) return path;

        return FindDescendant(path, "card_portraits") ?? path;
    }

    public static string ResolveCardsJson(string input)
    {
        var path = Clean(input);
        if (File.Exists(path)) return path;
        if (!Directory.Exists(path)) return path;

        var direct = Path.Combine(path, "cards.json");
        if (File.Exists(direct)) return direct;

        var zhs = Path.Combine(path, "zhs", "cards.json");
        if (File.Exists(zhs)) return zhs;

        return FindDescendantFile(path, "cards.json") ?? path;
    }

    private static string Clean(string input) => (input ?? "").Trim().TrimEnd('\\', '/');

    private static string? FindDescendant(string root, string name)
    {
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((root, 0));

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (depth >= MaxDepth) continue;

            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateDirectories(current).ToList();
            }
            catch
            {
                continue;
            }

            foreach (var child in children)
            {
                if (string.Equals(Path.GetFileName(child), name, StringComparison.OrdinalIgnoreCase))
                    return child;
                queue.Enqueue((child, depth + 1));
            }
        }

        return null;
    }

    private static string? FindDescendantFile(string root, string fileName)
    {
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((root, 0));

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (depth >= MaxDepth) continue;

            try
            {
                if (File.Exists(Path.Combine(current, fileName))
                    && Path.GetFileName(current).Equals("zhs", StringComparison.OrdinalIgnoreCase))
                    return Path.Combine(current, fileName);

                foreach (var child in Directory.EnumerateDirectories(current))
                    queue.Enqueue((child, depth + 1));
            }
            catch
            {
                // 跳过无权限目录
            }
        }

        return null;
    }
}
