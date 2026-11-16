using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CardArtTool;

public sealed class CardEntry
{
    /// <summary>卡池（小写，与 images/ 子目录同名）。</summary>
    public required string Pool { get; init; }

    /// <summary>卡牌文件名（不含扩展名），取自原版卡图，带下划线。</summary>
    public required string FileName { get; init; }

    public string ChineseName { get; init; } = "";

    public string? OriginalPath { get; set; }

    public string? OursPath { get; set; }

    public bool IsReplaced => OursPath != null;

    /// <summary>去掉下划线的形式，用于兼容历史素材命名。</summary>
    public string Compact => FileName.Replace("_", "");

    public string Title => ChineseName.Length > 0 ? ChineseName : FileName;
}

public sealed class CardIndex
{
    // 顺序与 mod 设置页一致
    public static readonly string[] PoolOrder =
    {
        "ironclad", "silent", "defect", "necrobinder", "regent", "fool",
        "colorless", "curse", "status", "token", "event", "quest",
    };

    public List<CardEntry> Cards { get; } = new();

    public List<string> Warnings { get; } = new();

    public void Rebuild(string originalRoot, string oursRoot, string cardsJson)
    {
        Cards.Clear();
        Warnings.Clear();

        var names = LoadCardNames(cardsJson);
        var byKey = new Dictionary<(string Pool, string Name), CardEntry>();

        if (Directory.Exists(originalRoot))
        {
            foreach (var pool in Pools(originalRoot))
                // 递归：愚者等卡池的图在 beta/ 子目录下
                foreach (var png in EnumeratePoolPngs(Path.Combine(originalRoot, pool)))
                {
                    var file = Path.GetFileNameWithoutExtension(png);
                    var entry = new CardEntry
                    {
                        Pool = pool,
                        FileName = file,
                        ChineseName = names.GetValueOrDefault(file.ToUpperInvariant(), ""),
                        OriginalPath = png,
                    };

                    // 同一卡池里 ashen_strike / ashenstrike 视为同一张卡
                    if (byKey.ContainsKey((pool, file)) || byKey.ContainsKey((pool, StripUnderscore(file))))
                        continue;

                    byKey[(pool, file)] = entry;
                    byKey[(pool, StripUnderscore(file))] = entry;
                    Cards.Add(entry);
                }
        }
        else
        {
            Warnings.Add($"原图目录不存在：{originalRoot}");
        }

        if (Directory.Exists(oursRoot))
        {
            foreach (var pool in Pools(oursRoot))
                foreach (var png in Directory.EnumerateFiles(Path.Combine(oursRoot, pool), "*.png"))
                {
                    var file = Path.GetFileNameWithoutExtension(png);
                    if (!byKey.TryGetValue((pool, file), out var entry))
                    {
                        entry = new CardEntry
                        {
                            Pool = pool,
                            FileName = file,
                            ChineseName = names.GetValueOrDefault(file.ToUpperInvariant(), ""),
                        };
                        byKey[(pool, file)] = entry;
                        byKey[(pool, StripUnderscore(file))] = entry;
                        Cards.Add(entry);
                    }

                    if (entry.OursPath != null)
                        Warnings.Add($"{pool} 存在重复命名：{Path.GetFileName(entry.OursPath)} 与 {Path.GetFileName(png)}");

                    entry.OursPath = png;
                }
        }
        else
        {
            Warnings.Add($"卡图目录不存在：{oursRoot}");
        }

        Cards.Sort((a, b) =>
        {
            var pa = Array.IndexOf(PoolOrder, a.Pool);
            var pb = Array.IndexOf(PoolOrder, b.Pool);
            if (pa < 0) pa = int.MaxValue;
            if (pb < 0) pb = int.MaxValue;
            return pa != pb ? pa.CompareTo(pb) : string.CompareOrdinal(a.FileName, b.FileName);
        });
    }

    /// <summary>判断某张卡该用哪种文件名落盘：沿用已有替图的命名，否则统一去下划线。</summary>
    public static string TargetFileName(CardEntry entry)
    {
        if (entry.OursPath != null) return Path.GetFileNameWithoutExtension(entry.OursPath);
        return entry.Compact;
    }

    private static string StripUnderscore(string name) => name.Replace("_", "");

    // 顶层图优先；beta 等子目录里的同名图只作补充
    private static IEnumerable<string> EnumeratePoolPngs(string poolDir)
    {
        if (!Directory.Exists(poolDir)) return Array.Empty<string>();

        try
        {
            return Directory.EnumerateFiles(poolDir, "*.png", SearchOption.AllDirectories)
                .OrderBy(p => p.Count(c => c == Path.DirectorySeparatorChar))
                .ThenBy(p => p, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<string> Pools(string root) =>
        Directory.EnumerateDirectories(root)
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!);

    private static Dictionary<string, string> LoadCardNames(string cardsJson)
    {
        var result = new Dictionary<string, string>();
        if (!File.Exists(cardsJson)) return result;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(cardsJson));
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                // 只取 "<ID>.title"，跳过 DINGY_DAGGER.title.dingy 这类子变体
                const string suffix = ".title";
                if (!prop.Name.EndsWith(suffix, StringComparison.Ordinal)) continue;
                var id = prop.Name[..^suffix.Length];
                if (id.Contains('.')) continue;
                if (prop.Value.ValueKind == JsonValueKind.String)
                    result[id] = prop.Value.GetString() ?? "";
            }
        }
        catch
        {
            // 名字只是辅助信息，读不到不影响主流程
        }

        return result;
    }
}
