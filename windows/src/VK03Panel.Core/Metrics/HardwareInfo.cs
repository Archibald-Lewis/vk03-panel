using System.Management;
using System.Runtime.InteropServices;
using System.Text;

namespace VK03Panel.Core.Metrics;

/// <summary>
/// Static hardware description read once at startup: CPU model and core counts, installed
/// memory modules and the discrete GPU name. Everything here is optional; failures degrade
/// to empty strings rather than failing the panel.
/// </summary>
public sealed class HardwareInfo
{
    public string CpuModel { get; private init; } = string.Empty;

    public int? PhysicalCores { get; private init; }

    public int? LogicalCores { get; private init; }

    public double? MemoryTotalGb { get; private init; }

    public string MemoryModules { get; private init; } = string.Empty;

    public string MemorySpeed { get; private init; } = string.Empty;

    public string GpuModel { get; private init; } = string.Empty;

    public static HardwareInfo Load()
    {
        var processorName = ReadProcessorName();
        var (physical, logical) = ReadProcessorCounts();
        var (totalGb, modules, speed) = ReadMemory();
        var gpu = ReadGpuName();

        return new HardwareInfo
        {
            CpuModel = ShortenCpuName(processorName),
            PhysicalCores = physical,
            LogicalCores = logical,
            MemoryTotalGb = totalGb,
            MemoryModules = modules,
            MemorySpeed = speed,
            GpuModel = gpu,
        };
    }

    private static string ReadProcessorName()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return (key?.GetValue("ProcessorNameString") as string ?? string.Empty).Trim();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>Removes vendor prefixes and core-count suffixes, mirroring the macOS app.</summary>
    public static string ShortenCpuName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var text = name
            .Replace("AMD ", string.Empty, StringComparison.Ordinal)
            .Replace("Intel(R) ", string.Empty, StringComparison.Ordinal)
            .Replace("(R)", string.Empty, StringComparison.Ordinal)
            .Replace("(TM)", string.Empty, StringComparison.Ordinal)
            .Replace(" CPU", string.Empty, StringComparison.Ordinal)
            .Replace(" Processor", string.Empty, StringComparison.Ordinal);

        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+\d+-Core\b", string.Empty);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s{2,}", " ");
        return text.Trim();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemLogicalProcessorInformationEx
    {
        public int Relationship;
        public uint Size;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool GetLogicalProcessorInformationEx(int relationshipType, IntPtr buffer, ref uint returnedLength);

    private static (int? Physical, int? Logical) ReadProcessorCounts()
    {
        var logical = Environment.ProcessorCount;
        try
        {
            uint length = 0;
            GetLogicalProcessorInformationEx(0, IntPtr.Zero, ref length);
            if (length == 0)
            {
                return (null, logical);
            }

            var buffer = Marshal.AllocHGlobal((int)length);
            try
            {
                if (!GetLogicalProcessorInformationEx(0, buffer, ref length))
                {
                    return (null, logical);
                }

                var physical = 0;
                var offset = 0;
                while (offset < length)
                {
                    var entry = Marshal.PtrToStructure<SystemLogicalProcessorInformationEx>(buffer + offset);
                    if (entry.Size == 0)
                    {
                        break;
                    }

                    if (entry.Relationship == 0)
                    {
                        physical++;
                    }

                    offset += (int)entry.Size;
                }

                return (physical > 0 ? physical : null, logical);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception)
        {
            return (null, logical);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    public static (ulong Total, ulong Available) ReadPhysicalMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            return (0, 0);
        }

        return (status.TotalPhys, status.AvailPhys);
    }

    private static (double? TotalGb, string Modules, string Speed) ReadMemory()
    {
        var (totalBytes, _) = ReadPhysicalMemory();
        var totalGb = totalBytes > 0 ? totalBytes / 1024d / 1024d / 1024d : (double?)null;
        var modules = string.Empty;
        var speed = string.Empty;

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Capacity, Speed, ConfiguredClockSpeed FROM Win32_PhysicalMemory");
            var sizes = new List<string>();
            var speeds = new List<int>();
            ulong moduleBytes = 0;
            foreach (var item in searcher.Get())
            {
                var capacity = ToUInt64(item["Capacity"]);
                if (capacity > 0)
                {
                    sizes.Add(FormatCapacity(capacity));
                    moduleBytes += capacity;
                }

                var configured = ToUInt64(item["ConfiguredClockSpeed"]);
                var rated = ToUInt64(item["Speed"]);
                var value = configured > 0 ? configured : rated;
                if (value > 0)
                {
                    speeds.Add((int)value);
                }

                item.Dispose();
            }

            if (moduleBytes > 0)
            {
                // Prefer the installed capacity so the panel shows “32 GB”, not 31.2 GiB.
                totalGb = moduleBytes / 1024d / 1024d / 1024d;
            }

            if (sizes.Count > 0)
            {
                modules = sizes.Distinct().Count() == 1
                    ? $"{sizes.Count} × {sizes[0]}"
                    : string.Join(" + ", sizes);
            }

            if (speeds.Count > 0)
            {
                speed = $"{speeds.Max()} MT/s";
            }
        }
        catch (Exception)
        {
            // WMI is optional.
        }

        return (totalGb, modules, speed);
    }

    private static string FormatCapacity(ulong bytes)
    {
        var gb = bytes / 1024d / 1024d / 1024d;
        return gb >= 1 ? $"{gb:0} GB" : $"{bytes / 1024d / 1024d:0} MB";
    }

    private static ulong ToUInt64(object? value)
    {
        try
        {
            return value is null ? 0 : Convert.ToUInt64(value);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static string ReadGpuName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_VideoController");
            var names = new List<string>();
            foreach (var item in searcher.Get())
            {
                var name = (item["Name"] as string ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(name) && !name.Contains("Basic Display", StringComparison.OrdinalIgnoreCase))
                {
                    names.Add(name);
                }

                item.Dispose();
            }

            // Prefer a discrete adapter over an integrated one.
            return names.FirstOrDefault(name =>
                       name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                       name.Contains("Radeon RX", StringComparison.OrdinalIgnoreCase) ||
                       name.Contains("Arc", StringComparison.OrdinalIgnoreCase))
                   ?? names.FirstOrDefault()
                   ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
