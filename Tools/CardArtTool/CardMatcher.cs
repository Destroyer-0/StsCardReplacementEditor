using System;
using System.IO;
using System.Text;

namespace CardArtTool;

// 批量拖入时的文件名匹配：忽略大小写、空格、下划线、连字符、点
public static class CardMatcher
{
    public static string Normalize(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            if (ch is ' ' or '_' or '-' or '.' or '\'' or '　') continue;
            builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString();
    }

    public static string NormalizeFileName(string filePath) =>
        Normalize(Path.GetFileNameWithoutExtension(filePath));
}
