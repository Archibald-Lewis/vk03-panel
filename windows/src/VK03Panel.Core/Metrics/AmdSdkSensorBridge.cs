using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace VK03Panel.Core.Metrics;

/// <summary>
/// Reads CPU telemetry straight from the AMD Ryzen Master Monitoring SDK that ships with the
/// AMD driver package. The exported <c>GetRmCpuParameters</c> entry point in <c>Platform.dll</c>
/// fills a CPU parameter block; its sensor doubles (temperature, core voltages) are reported in
/// physical units. The driver device only grants access to elevated processes, therefore this
/// bridge is only available when the application runs as administrator.
/// </summary>
public sealed class AmdSdkSensorBridge : ISensorBridge, IDisposable
{
    private const int BufferLength = 0x200;
    private const int DoubleScanStart = 0x40;
    private const int DoubleScanEnd = 0x100;

    private static readonly string[] CandidatePaths =
    {
        Path.Combine(Environment.SystemDirectory, "Platform.dll"),
        @"C:\Program Files\AMD\Performance Profile Client\Platform.dll",
        @"C:\Program Files\AMD\CNext\CNext\Platform.dll",
    };

    private readonly object _sync = new();
    private IntPtr _module;
    private Delegate? _call;
    private bool _initialized;
    private bool _available;
    private string _lastError = string.Empty;

    public string Name => "AMD Ryzen Master SDK";

    public bool IsAvailable
    {
        get
        {
            lock (_sync)
            {
                if (!_initialized)
                {
                    InitializeInternal();
                }

                return _available;
            }
        }
    }

    public string LastError
    {
        get
        {
            lock (_sync)
            {
                return _lastError;
            }
        }
    }

    public string LibraryPath { get; private set; } = string.Empty;

    public void Initialize()
    {
        lock (_sync)
        {
            InitializeInternal();
        }
    }

    private void InitializeInternal()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        foreach (var path in CandidatePaths)
        {
            try
            {
                var module = NativeMethods.LoadLibrary(path);
                if (module == IntPtr.Zero)
                {
                    _lastError = $"无法加载 {path}（Win32 {Marshal.GetLastWin32Error()}）";
                    continue;
                }

                var address = NativeMethods.GetProcAddress(module, "GetRmCpuParameters");
                if (address == IntPtr.Zero)
                {
                    NativeMethods.FreeLibrary(module);
                    _lastError = $"{path} 缺少 GetRmCpuParameters";
                    continue;
                }

                var call = Marshal.GetDelegateForFunctionPointer<GetCpuParametersDelegate>(address);
                var buffer = Marshal.AllocHGlobal(BufferLength);
                try
                {
                    Zero(buffer);
                    var result = call(buffer);
                    if (result != 0)
                    {
                        _lastError = $"GetRmCpuParameters 返回 {result}（通常表示需要管理员权限）";
                        NativeMethods.FreeLibrary(module);
                        continue;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }

                _module = module;
                _call = call;
                _available = true;
                _lastError = string.Empty;
                LibraryPath = path;
                return;
            }
            catch (Exception error)
            {
                _lastError = error.Message;
            }
        }
    }

    /// <summary>Returns the package temperature in °C, or null when the SDK is unavailable.</summary>
    public double? ReadCpuTemperature()
    {
        var sample = Read();
        return sample?.TemperatureCelsius;
    }

    /// <summary>Reads the parameter block; exposes every plausible sensor value for diagnostics.</summary>
    public AmdSdkReading? Read()
    {
        lock (_sync)
        {
            if (!_initialized)
            {
                InitializeInternal();
            }

            if (!_available || _call is null)
            {
                return null;
            }

            var buffer = Marshal.AllocHGlobal(BufferLength);
            try
            {
                Zero(buffer);
                int result;
                try
                {
                    result = ((GetCpuParametersDelegate)_call)(buffer);
                }
                catch (Exception error)
                {
                    _lastError = error.Message;
                    return null;
                }

                if (result != 0)
                {
                    _lastError = $"GetRmCpuParameters 返回 {result}";
                    return null;
                }

                var bytes = new byte[BufferLength];
                Marshal.Copy(buffer, bytes, 0, BufferLength);
                var doubles = new List<AmdSensorValue>();
                var floats = new List<AmdSensorValue>();

                for (var offset = DoubleScanStart; offset + 8 <= DoubleScanEnd; offset += 8)
                {
                    var value = BitConverter.ToDouble(bytes, offset);
                    if (double.IsFinite(value) && Math.Abs(value) < 100000)
                    {
                        doubles.Add(new AmdSensorValue(offset, value));
                    }
                }

                for (var offset = 0; offset + 4 <= 0x80; offset += 4)
                {
                    var value = BitConverter.ToSingle(bytes, offset);
                    if (float.IsFinite(value) && Math.Abs(value) < 100000)
                    {
                        floats.Add(new AmdSensorValue(offset, value));
                    }
                }

                // CPU package temperature is a double in physical °C; core voltages (0.5–2.0)
                // and peak speeds are outside the plausible window.
                // Prefer the 8-byte sensor slots that the SDK uses for voltages and the
                // package temperature; two adjacent floats can form a plausible double when
                // read as a pair, so the wider scan is only a fallback.
                var preferredOffsets = new[] { 0x80, 0x88, 0x90, 0x98, 0xa0, 0xa8, 0xb0 };
                AmdSensorValue? temperature = null;
                foreach (var offset in preferredOffsets)
                {
                    var candidate = doubles.FirstOrDefault(value => value.Offset == offset && value.Value is > 10 and < 120);
                    if (candidate.Value is > 10 and < 120)
                    {
                        temperature = candidate;
                        break;
                    }
                }

                if (temperature is null)
                {
                    foreach (var candidate in doubles)
                    {
                        if (candidate.Value is > 10 and < 120)
                        {
                            temperature = candidate;
                            break;
                        }
                    }
                }

                if (temperature is null)
                {
                    foreach (var candidate in floats)
                    {
                        if (candidate.Value is > 20 and < 110)
                        {
                            temperature = new AmdSensorValue(candidate.Offset | 0x4000, candidate.Value);
                            break;
                        }
                    }
                }

                return new AmdSdkReading(
                    DateTime.UtcNow,
                    temperature?.Value,
                    temperature?.Offset,
                    doubles,
                    floats);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    private static void Zero(IntPtr buffer)
    {
        for (var index = 0; index < BufferLength; index++)
        {
            Marshal.WriteByte(buffer, index, 0);
        }
    }

    /// <summary>Diagnostic dump used by --amd-dump and the settings page.</summary>
    public string Describe()
    {
        if (!IsAvailable)
        {
            return $"不可用（{LastError}）";
        }

        var reading = Read();
        if (reading is null)
        {
            return $"可用但读取失败（{LastError}）";
        }

        var builder = new StringBuilder();
        builder.Append(reading.TemperatureCelsius.HasValue
            ? $"CPU {reading.TemperatureCelsius.Value:0.0} °C"
            : "未找到温度字段");
        builder.Append($" · {LibraryPath}");
        return builder.ToString();
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_module != IntPtr.Zero)
            {
                try
                {
                    NativeMethods.FreeLibrary(_module);
                }
                catch (Exception)
                {
                    // Ignore unload failures.
                }

                _module = IntPtr.Zero;
            }

            _call = null;
            _available = false;
            _initialized = true;
        }
    }

    private delegate int GetCpuParametersDelegate(IntPtr buffer);

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr LoadLibrary(string fileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr module, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeLibrary(IntPtr module);
    }
}

public readonly record struct AmdSensorValue(int Offset, double Value);

public sealed record AmdSdkReading(
    DateTime Timestamp,
    double? TemperatureCelsius,
    int? TemperatureOffset,
    IReadOnlyList<AmdSensorValue> Doubles,
    IReadOnlyList<AmdSensorValue> Floats);
