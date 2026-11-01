using System.Runtime.InteropServices;
using System.Text;

namespace VK03Panel.Core.Metrics;

/// <summary>Minimal NVML surface (shipped with the NVIDIA driver) for GPU load and temperature.</summary>
internal static class Nvml
{
    private const string Library = "nvml.dll";

    public const uint TemperatureGpu = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct Utilization
    {
        public uint Gpu;
        public uint Memory;
    }

    [DllImport(Library)]
    public static extern int nvmlInit_v2();

    [DllImport(Library)]
    public static extern int nvmlShutdown();

    [DllImport(Library)]
    public static extern int nvmlDeviceGetCount_v2(out uint count);

    [DllImport(Library)]
    public static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);

    [DllImport(Library, CharSet = CharSet.Ansi)]
    public static extern int nvmlDeviceGetName(IntPtr device, StringBuilder name, uint length);

    [DllImport(Library)]
    public static extern int nvmlDeviceGetUtilizationRates(IntPtr device, out Utilization utilization);

    [DllImport(Library)]
    public static extern int nvmlDeviceGetTemperature(IntPtr device, uint sensorType, out uint temperature);

    [DllImport(Library)]
    public static extern int nvmlDeviceGetMemoryInfo(IntPtr device, out MemoryInfo memory);

    [StructLayout(LayoutKind.Sequential)]
    public struct MemoryInfo
    {
        public ulong Total;
        public ulong Free;
        public ulong Used;
    }

    [DllImport(Library)]
    public static extern int nvmlDeviceGetPowerUsage(IntPtr device, out uint milliwatts);
}

/// <summary>Samples the first NVIDIA GPU through NVML. Silent no-op when no NVIDIA driver is present.</summary>
public sealed class GpuSampler : IDisposable
{
    private IntPtr _device = IntPtr.Zero;
    private bool _initialized;

    public string Name { get; private set; } = string.Empty;

    public bool IsAvailable => _initialized && _device != IntPtr.Zero;

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        try
        {
            if (Nvml.nvmlInit_v2() != 0)
            {
                _initialized = true;
                return;
            }

            if (Nvml.nvmlDeviceGetCount_v2(out var count) != 0 || count == 0)
            {
                _initialized = true;
                return;
            }

            if (Nvml.nvmlDeviceGetHandleByIndex_v2(0, out var device) != 0 || device == IntPtr.Zero)
            {
                _initialized = true;
                return;
            }

            _device = device;
            var builder = new StringBuilder(96);
            if (Nvml.nvmlDeviceGetName(device, builder, (uint)builder.Capacity) == 0)
            {
                Name = builder.ToString().Trim();
            }
        }
        catch (DllNotFoundException)
        {
            // No NVIDIA driver present.
        }
        catch (BadImageFormatException)
        {
            // Architecture mismatch.
        }
        catch (EntryPointNotFoundException)
        {
            // Older NVML without the v2 entry points.
        }
        finally
        {
            _initialized = true;
        }
    }

    public (double? Load, double? TemperatureCelsius)? Sample()
    {
        if (!IsAvailable)
        {
            return null;
        }

        double? load = null;
        double? temperature = null;

        try
        {
            if (Nvml.nvmlDeviceGetUtilizationRates(_device, out var utilization) == 0)
            {
                load = utilization.Gpu / 100d;
            }

            if (Nvml.nvmlDeviceGetTemperature(_device, Nvml.TemperatureGpu, out var value) == 0 && value > 0)
            {
                temperature = value;
            }
        }
        catch (Exception)
        {
            return null;
        }

        return (load, temperature);
    }

    public void Dispose()
    {
        if (!_initialized)
        {
            return;
        }

        try
        {
            Nvml.nvmlShutdown();
        }
        catch (Exception)
        {
            // Ignore shutdown failures.
        }

        _initialized = false;
        _device = IntPtr.Zero;
    }
}
