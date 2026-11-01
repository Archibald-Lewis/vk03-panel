using System.IO;
using System.Text.Json;
namespace VK03Panel.Core.Metrics;

/// <summary>One sampling pass of the live metrics shown on the panel.</summary>
public sealed class MetricsSnapshot
{
    public double? CpuLoad { get; init; }

    public double? CpuTemperatureCelsius { get; init; }

    public double? CpuFrequencyGhz { get; init; }

    public double? MemoryUsedGb { get; init; }

    public double? MemoryTotalGb { get; init; }

    public double? GpuLoad { get; init; }

    public double? GpuTemperatureCelsius { get; init; }

    public bool HasLiveMetrics => CpuLoad.HasValue && CpuFrequencyGhz.HasValue;

    public static readonly MetricsSnapshot Empty = new();
}

/// <summary>
/// Combines the Windows-native collectors with the AMD Ryzen Master SDK (preferred, needs
/// administrator access), then HWiNFO shared memory and LibreHardwareMonitor WMI as fallbacks.
/// Missing values stay null; the themes render a dash instead of inventing data.
/// </summary>
public sealed class MetricsAggregator : IDisposable
{
    private readonly CpuLoadSampler _cpuLoad = new();
    private readonly CpuFrequencySampler _cpuFrequency = new();
    private readonly GpuSampler _gpu = new();
    private AmdSdkSensorBridge? _amd;
    private ISensorBridge? _sensor;
    private bool _initialized;

    public HardwareInfo Hardware { get; private set; } = HardwareInfo.Load();

    /// <summary>Label shown in the panel status chip, for example “内置” or “HWiNFO”.</summary>
    public string DataSourceLabel => _sensor?.Name ?? "内置";

    public string TemperatureSourceName => _sensor?.Name ?? "未接入";

    public bool HasTemperatureSource => _sensor is not null;

    public bool HasGpuTelemetry => _gpu.IsAvailable;

    /// <summary>True when the AMD SDK bridge is supplying the temperature.</summary>
    public bool AmdSdkActive { get; private set; }

    /// <summary>Human readable AMD SDK state (used by the data page).</summary>
    public string AmdSdkStatus { get; private set; } = "未检测";

    public string GpuName =>
        !string.IsNullOrEmpty(_gpu.Name) ? _gpu.Name : Hardware.GpuModel;

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        Hardware = Metrics.HardwareInfo.Load();
        _gpu.Initialize();

        var amd = new AmdSdkSensorBridge();
        amd.Initialize();
        if (amd.IsAvailable)
        {
            _amd = amd;
            _sensor = amd;
            AmdSdkActive = true;
            AmdSdkStatus = "已启用（管理员）";
        }
        else
        {
            AmdSdkStatus = string.IsNullOrEmpty(amd.LastError) ? "不可用" : amd.LastError;
            amd.Dispose();
        }

        _sensor ??= SensorBridgeDetector.Detect();
        _initialized = true;
    }

    public MetricsSnapshot Sample()
    {
        if (!_initialized)
        {
            Initialize();
        }

        var load = _cpuLoad.Sample();
        var frequency = _cpuFrequency.Sample();
        var gpu = _gpu.Sample();

        var (totalBytes, availableBytes) = Metrics.HardwareInfo.ReadPhysicalMemory();
        double? usedGb = totalBytes > 0 ? (totalBytes - availableBytes) / 1024d / 1024d / 1024d : null;
        double? totalGb = Hardware.MemoryTotalGb ??
                          (totalBytes > 0 ? totalBytes / 1024d / 1024d / 1024d : null);

        return new MetricsSnapshot
        {
            CpuLoad = load,
            CpuTemperatureCelsius = _sensor?.ReadCpuTemperature(),
            CpuFrequencyGhz = frequency,
            MemoryUsedGb = usedGb,
            MemoryTotalGb = totalGb,
            GpuLoad = gpu?.Load,
            GpuTemperatureCelsius = gpu?.TemperatureCelsius,
        };
    }

    /// <summary>Writes the AMD sensor detail (used once when running elevated) for verification.</summary>
    public bool TryWriteAmdDump(string path)
    {
        var reading = _amd?.Read();
        if (reading is null)
        {
            return false;
        }

        var report = new
        {
            timestamp = reading.Timestamp,
            temperature = reading.TemperatureCelsius,
            temperatureOffset = reading.TemperatureOffset,
            library = _amd?.LibraryPath,
            doubles = reading.Doubles.Select(value => new { offset = $"0x{value.Offset:x}", value = value.Value }),
            floats = reading.Floats.Select(value => new { offset = $"0x{value.Offset:x}", value = value.Value }),
        };
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return true;
    }

    /// <summary>Human-readable diagnostics for the settings page and --diagnose.</summary>
    public string Describe()
    {
        if (!_initialized)
        {
            Initialize();
        }

        var lines = new List<string>
        {
            $"CPU        {Hardware.CpuModel}（{Hardware.PhysicalCores} 核 / {Hardware.LogicalCores} 线程）",
            $"内存       {Hardware.MemoryTotalGb:0.#} GB · {Hardware.MemoryModules} · {Hardware.MemorySpeed}",
            $"GPU        {GpuName}（NVML {(HasGpuTelemetry ? "可用" : "不可用")}）",
            $"AMD 传感器 {AmdSdkStatus}",
            $"温度来源   {TemperatureSourceName}",
            $"数据源标签 {DataSourceLabel}",
        };

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>Re-runs detection (used by the data page refresh button).</summary>
    public void Reinitialize()
    {
        Dispose();
        _initialized = false;
        AmdSdkActive = false;
        AmdSdkStatus = "未检测";
        Initialize();
    }

    public void Dispose()
    {
        _gpu.Dispose();
        if (_sensor is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _amd = null;
        _sensor = null;
        _initialized = false;
        AmdSdkActive = false;
    }
}
