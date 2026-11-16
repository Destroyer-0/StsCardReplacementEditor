using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CardArtTool;

public static class ExportRunner
{
    /// <summary>仓库根目录：以卡图目录的上一级为准。</summary>
    public static string? FindRepoRoot(string imagesDir)
    {
        var dir = new DirectoryInfo(imagesDir);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "导出pck.bat"))) return dir.FullName;
            dir = dir.Parent;
        }

        return null;
    }

    public static async Task<(bool Ok, string Log)> RunAsync(string repoRoot)
    {
        var bat = Path.Combine(repoRoot, "导出pck.bat");
        if (!File.Exists(bat)) return (false, "找不到 导出pck.bat：" + bat);

        var startInfo = new ProcessStartInfo("cmd.exe")
        {
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add($"\"{bat}\" -nopause");

        using var process = Process.Start(startInfo);
        if (process == null) return (false, "无法启动导出脚本");

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var log = output + error;
        return (process.ExitCode == 0, log.Length > 4000 ? log[^4000..] : log);
    }
}
