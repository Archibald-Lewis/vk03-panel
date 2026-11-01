using System.Windows.Media;
using VK03Panel.Core.Themes;

namespace VK03Panel.Core.Cli;

/// <summary>Synthetic, clearly-labelled data for documentation images and UI previews.</summary>
public static class DemoData
{
    /// <summary>2026-10-30 17:30 local time, matching the reference screenshots.</summary>
    public static readonly DateTime DemoTimestamp = new(2026, 10, 30, 17, 30, 0, DateTimeKind.Local);

    public static PanelViewModel Create(bool dark, Color? accent = null)
    {
        return new PanelViewModel
        {
            Time = DemoTimestamp,
            DarkMode = dark,
            CpuTemperatureCelsius = 48,
            CpuFrequencyGhz = 4.65,
            CpuLoad = 0.23,
            GpuLoad = 0.18,
            GpuTemperatureCelsius = 52,
            MemoryUsedGb = 12.4,
            MemoryTotalGb = 32,
            CpuModel = "Ryzen 7 · Demo",
            PhysicalCores = 8,
            LogicalCores = 16,
            MemoryModules = "2 × 16 GB",
            MemorySpeed = "5600 MT/s",
            GpuModel = "GeForce RTX · Demo",
            DataSourceLabel = "DEMO",
            HasLiveMetrics = true,
            Demo = true,
            Media = new PanelMediaState
            {
                Title = "VK03 Panel",
                Artist = "演示数据",
                Album = "Demo",
                Playing = false,
                DurationSeconds = 210,
                ElapsedSeconds = 88,
                HasSession = true,
                CanSeek = true,
            },
            Volume = 0.42,
            Muted = false,
            CanLock = true,
            LockProgress = 0,
            AccentColor = accent,
            CpuLoadHistory = Wave(0.23, 0.09, 0.4),
            GpuLoadHistory = Wave(0.18, 0.07, 1.7),
            MemoryHistory = Wave(0.39, 0.02, 2.9),
        };
    }

    /// <summary>Deterministic smooth history so demo images show realistic sparklines.</summary>
    private static IReadOnlyList<double> Wave(double centre, double amplitude, double phase)
    {
        var values = new double[40];
        for (var index = 0; index < values.Length; index++)
        {
            var wave = Math.Sin(phase + (index * 0.32)) * amplitude;
            var ripple = Math.Sin((index * 0.9) + phase) * (amplitude * 0.25);
            values[index] = Math.Clamp(centre + wave + ripple, 0.02, 0.95);
        }

        return values;
    }
}
