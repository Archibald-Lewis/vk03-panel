using System.Windows.Media;

namespace VK03Panel.Core.Themes;

/// <summary>Now-playing data rendered on the panel.</summary>
public sealed class PanelMediaState
{
    public static readonly PanelMediaState Empty = new();

    public string Title { get; init; } = string.Empty;

    public string Artist { get; init; } = string.Empty;

    public string Album { get; init; } = string.Empty;

    public string SourceApp { get; init; } = string.Empty;

    public bool Playing { get; init; }

    public double DurationSeconds { get; init; }

    public double ElapsedSeconds { get; init; }

    public ImageSource? Artwork { get; init; }

    public bool HasSession { get; init; }

    public double Progress => DurationSeconds <= 0 ? 0 : Math.Clamp(ElapsedSeconds / DurationSeconds, 0, 1);
}

/// <summary>
/// Immutable snapshot handed to a theme for one frame. Themes never read system state
/// directly, so previews, demos and hardware rendering all use exactly the same path.
/// </summary>
public sealed class PanelViewModel
{
    public DateTime Time { get; init; } = DateTime.Now;

    public bool DarkMode { get; init; } = true;

    public double? CpuTemperatureCelsius { get; init; }

    public double? CpuFrequencyGhz { get; init; }

    public double? CpuLoad { get; init; }

    public double? GpuLoad { get; init; }

    public double? GpuTemperatureCelsius { get; init; }

    public double? MemoryUsedGb { get; init; }

    public double? MemoryTotalGb { get; init; }

    public string CpuModel { get; init; } = string.Empty;

    public int? PhysicalCores { get; init; }

    public int? LogicalCores { get; init; }

    public string MemoryModules { get; init; } = string.Empty;

    public string MemorySpeed { get; init; } = string.Empty;

    public string GpuModel { get; init; } = string.Empty;

    /// <summary>Short label shown in the status chip, for example “内置”, “HWiNFO” or “DEMO”.</summary>
    public string DataSourceLabel { get; init; } = string.Empty;

    public bool HasLiveMetrics { get; init; }

    /// <summary>True when the USB panel is connected; drives the Windows 11 status pill.</summary>
    public bool PanelConnected { get; init; } = true;

    public bool Demo { get; init; }

    public PanelMediaState Media { get; init; } = PanelMediaState.Empty;

    public double? Volume { get; init; }

    public bool Muted { get; init; }

    public bool CanLock { get; init; }

    /// <summary>0..1 while the lock button is held.</summary>
    public double LockProgress { get; init; }

    public PanelAction? PressedAction { get; init; }

    /// <summary>Accent colour for the Windows 11 theme; ignored by the macOS theme.</summary>
    public Color? AccentColor { get; init; }

    /// <summary>Recent CPU load samples (0..1), oldest first; used for sparklines.</summary>
    public IReadOnlyList<double> CpuLoadHistory { get; init; } = Array.Empty<double>();

    /// <summary>Recent GPU utilisation samples (0..1), oldest first.</summary>
    public IReadOnlyList<double> GpuLoadHistory { get; init; } = Array.Empty<double>();

    /// <summary>Recent memory usage fractions (0..1), oldest first.</summary>
    public IReadOnlyList<double> MemoryHistory { get; init; } = Array.Empty<double>();

    public double MemoryFraction
    {
        get
        {
            var used = MemoryUsedGb ?? 0;
            var total = Math.Max(1, MemoryTotalGb ?? 1);
            return Math.Clamp(used / total, 0, 1);
        }
    }
}
