using System.Runtime.InteropServices;

namespace VK03Panel.Core.Metrics;

/// <summary>Total CPU utilisation from GetSystemTimes deltas (no administrator rights needed).</summary>
public sealed class CpuLoadSampler
{
    private ulong _previousIdle;
    private ulong _previousKernel;
    private ulong _previousUser;
    private bool _hasPrevious;

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;

        public ulong Value => ((ulong)High << 32) | Low;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    public double? Sample()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return null;
        }

        var idleNow = idle.Value;
        var kernelNow = kernel.Value;
        var userNow = user.Value;

        if (!_hasPrevious)
        {
            _previousIdle = idleNow;
            _previousKernel = kernelNow;
            _previousUser = userNow;
            _hasPrevious = true;
            return null;
        }

        var idleDelta = idleNow - _previousIdle;
        var kernelDelta = kernelNow - _previousKernel;
        var userDelta = userNow - _previousUser;

        _previousIdle = idleNow;
        _previousKernel = kernelNow;
        _previousUser = userNow;

        var total = kernelDelta + userDelta;
        if (total == 0)
        {
            return null;
        }

        var busy = total > idleDelta ? total - idleDelta : 0;
        return Math.Clamp(busy / (double)total, 0, 1);
    }
}

/// <summary>
/// Average current CPU clock in GHz. Uses NtPowerInformation(ProcessorInformation), which
/// reports the live per-core clock without driver access.
/// </summary>
public sealed class CpuFrequencySampler
{
    private const int ProcessorInformation = 11;
    private const int FieldsPerProcessor = 6;

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern int CallNtPowerInformation(
        int informationLevel,
        IntPtr inputBuffer,
        uint inputBufferSize,
        IntPtr outputBuffer,
        uint outputBufferSize);

    private readonly int _processorCount;

    public CpuFrequencySampler()
    {
        _processorCount = Math.Max(1, Environment.ProcessorCount);
    }

    public double? Sample()
    {
        var length = _processorCount * FieldsPerProcessor * sizeof(uint);
        var buffer = Marshal.AllocHGlobal(length);
        try
        {
            var result = CallNtPowerInformation(ProcessorInformation, IntPtr.Zero, 0, buffer, (uint)length);
            if (result != 0)
            {
                return Fallback();
            }

            ulong total = 0;
            var counted = 0;
            for (var index = 0; index < _processorCount; index++)
            {
                var currentMhz = (uint)Marshal.ReadInt32(buffer, ((index * FieldsPerProcessor) + 2) * sizeof(uint));
                if (currentMhz > 0)
                {
                    total += currentMhz;
                    counted++;
                }
            }

            if (counted == 0)
            {
                return Fallback();
            }

            return total / (double)counted / 1000d;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static double? Fallback()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            if (key?.GetValue("~MHz") is int mhz && mhz > 0)
            {
                return mhz / 1000d;
            }
        }
        catch (Exception)
        {
            // Ignore registry failures.
        }

        return null;
    }
}
