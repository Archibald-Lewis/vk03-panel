using System.IO;
using VK03Panel.Core.Rendering;
using VK03Panel.Core.Themes;

namespace VK03Panel.Core.Cli;

/// <summary>Renders synthetic documentation images without touching hardware or user data.</summary>
public static class DemoCommand
{
    public static int Run(string[] args, TextWriter output)
    {
        var index = Array.FindIndex(args, argument => argument.Equals("--demo", StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index + 1 >= args.Length)
        {
            output.WriteLine("用法：--demo <输出.png> [--theme mac|win11|nebula|immersive] [--dark|--light] [--rotation 0|90|180|270]");
            return 1;
        }

        var path = args[index + 1];
        var themeId = ParseValue(args, "--theme") ?? PanelThemes.MacId;
        if (!PanelThemes.Exists(themeId))
        {
            output.WriteLine($"未知主题：{themeId}（可用：{string.Join(", ", PanelThemes.All.Select(theme => theme.Id))}）");
            return 1;
        }

        var dark = !args.Contains("--light", StringComparer.OrdinalIgnoreCase);
        if (args.Contains("--dark", StringComparer.OrdinalIgnoreCase))
        {
            dark = true;
        }

        var rotation = ParseInt(args, "--rotation", 90);
        if (!BgrFrameEncoder.IsSupportedRotation(rotation))
        {
            output.WriteLine($"不支持的方向：{rotation}");
            return 1;
        }

        var theme = PanelThemes.Get(themeId);
        var model = DemoData.Create(dark);
        var frame = PanelRenderer.Render(theme, model, rotation);
        PanelRenderer.SavePng(frame.Bitmap, path);
        output.WriteLine($"已生成 {Path.GetFullPath(path)}（{theme.DisplayName} · {(dark ? "深色" : "浅色")} · {frame.Width}×{frame.Height}）");
        return 0;
    }

    private static string? ParseValue(string[] args, string name)
    {
        var index = Array.FindIndex(args, argument => argument.Equals(name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static int ParseInt(string[] args, string name, int fallback)
    {
        var value = ParseValue(args, name);
        return value is not null && int.TryParse(value, out var parsed) ? parsed : fallback;
    }
}
