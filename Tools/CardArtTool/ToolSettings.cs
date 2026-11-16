using System;
using System.IO;
using System.Text.Json;

namespace CardArtTool;

public sealed class ToolSettings
{
    public string OriginalRoot { get; set; } = "";
    public string OursRoot { get; set; } = "";
    public string CardsJson { get; set; } = "";

    // 配置跟着仓库走：从 exe 所在目录向上找带 Tools/CardArtTool 的仓库根
    private static string FilePath => Path.Combine(ResolveSettingsDir(), "settings.json");

    private static string ResolveSettingsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var toolDir = Path.Combine(dir.FullName, "Tools", "CardArtTool");
            if (Directory.Exists(toolDir)) return toolDir;
            dir = dir.Parent;
        }

        return AppContext.BaseDirectory;
    }

    public static ToolSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<ToolSettings>(File.ReadAllText(FilePath));
                if (loaded != null) return loaded;
            }
        }
        catch
        {
            // 配置坏了就用默认值
        }

        return new ToolSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true,
            }));
        }
        catch
        {
            // 存不了不影响使用
        }
    }
}
