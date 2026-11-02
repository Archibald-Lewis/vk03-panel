using System.IO;
using VK03Panel.Core.Audio;
using VK03Panel.Core.Media;
using VK03Panel.Core.Metrics;
using VK03Panel.Core.Platform;
using VK03Panel.Core.Rendering;
using VK03Panel.Core.Themes;

namespace VK03Panel.Core.Cli;

/// <summary>Renders a preview PNG from the current machine's real data without touching the panel.</summary>
public static class RenderCommand
{
    public static int Run(string[] args, TextWriter output)
    {
        var index = Array.FindIndex(args, argument => argument.Equals("--render", StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index + 1 >= args.Length)
        {
            output.WriteLine("用法：--render <输出.png> [--theme mac|win11|nebula|immersive|sketch|anime] [--dark|--light]");
            return 1;
        }

        var path = args[index + 1];
        var theme = PanelThemes.Get(ParseValue(args, "--theme") ?? PanelThemes.Win11Id);
        var dark = !args.Contains("--light", StringComparer.OrdinalIgnoreCase);
        var rotation = ParseInt(args, "--rotation", 90);

        using var metrics = new MetricsAggregator();
        metrics.Initialize();
        _ = metrics.Sample();
        Thread.Sleep(1100);
        var snapshot = metrics.Sample();

        using var media = new MediaWatcher();
        try
        {
            Task.Run(() => media.InitializeAsync()).GetAwaiter().GetResult();
            Thread.Sleep(400);
        }
        catch (Exception)
        {
            // Media is optional.
        }

        var audio = new EndpointVolume();
        var volume = audio.Read();

        var model = PanelViewModelFactory.Create(
            snapshot,
            metrics.Hardware,
            metrics.GpuName,
            metrics.DataSourceLabel,
            media.Snapshot(),
            volume?.Volume,
            volume?.Muted ?? false,
            LockScreen.IsAvailable,
            dark,
            panelConnected: false,
            DateTime.Now);

        var frame = PanelRenderer.Render(theme, model, rotation);
        PanelRenderer.SavePng(frame.Bitmap, path);
        output.WriteLine($"已生成 {Path.GetFullPath(path)}（{theme.DisplayName} · {frame.Width}×{frame.Height}）");
        return 0;
    }

    private static string? ParseValue(string[] args, string name)
    {
        var position = Array.FindIndex(args, argument => argument.Equals(name, StringComparison.OrdinalIgnoreCase));
        return position >= 0 && position + 1 < args.Length ? args[position + 1] : null;
    }

    private static int ParseInt(string[] args, string name, int fallback)
    {
        var value = ParseValue(args, name);
        return value is not null && int.TryParse(value, out var parsed) ? parsed : fallback;
    }
}
