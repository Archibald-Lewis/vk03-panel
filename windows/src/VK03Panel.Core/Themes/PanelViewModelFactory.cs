using System.Windows.Media;
using VK03Panel.Core.Metrics;

namespace VK03Panel.Core.Themes;

/// <summary>Composes the immutable view model used by both themes and all output paths.</summary>
public static class PanelViewModelFactory
{
    public static PanelViewModel Create(
        MetricsSnapshot snapshot,
        HardwareInfo hardware,
        string gpuName,
        string dataSourceLabel,
        PanelMediaState media,
        double? volume,
        bool muted,
        bool canLock,
        bool darkMode,
        bool panelConnected,
        DateTime now,
        bool demo = false,
        Color? accent = null,
        double lockProgress = 0,
        PanelAction? pressedAction = null,
        IReadOnlyList<double>? cpuLoadHistory = null,
        IReadOnlyList<double>? gpuLoadHistory = null,
        IReadOnlyList<double>? memoryHistory = null,
        string? playbackMessage = null)
    {
        return new PanelViewModel
        {
            Time = now,
            DarkMode = darkMode,
            CpuTemperatureCelsius = snapshot.CpuTemperatureCelsius,
            CpuFrequencyGhz = snapshot.CpuFrequencyGhz,
            CpuLoad = snapshot.CpuLoad,
            GpuLoad = snapshot.GpuLoad,
            GpuTemperatureCelsius = snapshot.GpuTemperatureCelsius,
            MemoryUsedGb = snapshot.MemoryUsedGb,
            MemoryTotalGb = snapshot.MemoryTotalGb ?? hardware.MemoryTotalGb,
            CpuModel = hardware.CpuModel,
            PhysicalCores = hardware.PhysicalCores,
            LogicalCores = hardware.LogicalCores,
            MemoryModules = hardware.MemoryModules,
            MemorySpeed = hardware.MemorySpeed,
            GpuModel = string.IsNullOrEmpty(gpuName) ? hardware.GpuModel : gpuName,
            DataSourceLabel = dataSourceLabel,
            HasLiveMetrics = snapshot.HasLiveMetrics,
            PanelConnected = panelConnected,
            Demo = demo,
            Media = media,
            PlaybackMessage = playbackMessage,
            Volume = volume,
            Muted = muted,
            CanLock = canLock,
            LockProgress = lockProgress,
            PressedAction = pressedAction,
            AccentColor = accent,
            CpuLoadHistory = cpuLoadHistory ?? Array.Empty<double>(),
            GpuLoadHistory = gpuLoadHistory ?? Array.Empty<double>(),
            MemoryHistory = memoryHistory ?? Array.Empty<double>(),
        };
    }
}
