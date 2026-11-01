using System.IO.MemoryMappedFiles;
using System.Management;
using System.Text;

namespace VK03Panel.Core.Metrics;

/// <summary>CPU package temperature provider. The panel never samples sensors itself.</summary>
public interface ISensorBridge
{
    string Name { get; }

    double? ReadCpuTemperature();
}

/// <summary>
/// Reads HWiNFO's shared memory block ("Global\HWiNFO_SENS_SM2"). This is the recommended
/// bridge on Windows 11 builds where Memory Integrity blocks Ring-0 sensor drivers.
/// </summary>
public sealed class HwinfoSensorBridge : ISensorBridge, IDisposable
{
    private const uint Signature = 0x53695748; // 'HWiS'
    private const uint CpuTemperatureReadingId = 0x00010000;

    private readonly string _mappingName;
    private MemoryMappedFile? _file;
    private MemoryMappedViewAccessor? _view;
    private int _sensorOffset;
    private int _readingOffset;
    private int _sensorSize;
    private int _readingSize;
    private int _sensorCount;
    private int _readingCount;
    private int _valueOffset = 284;

    public HwinfoSensorBridge(string mappingName = @"Global\HWiNFO_SENS_SM2")
    {
        _mappingName = mappingName;
    }

    public string Name => "HWiNFO";

    public bool IsOpen => _view is not null;

    public bool TryOpen()
    {
        try
        {
            _file = MemoryMappedFile.OpenExisting(_mappingName, MemoryMappedFileRights.Read);
            _view = _file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            return ParseHeader();
        }
        catch (Exception)
        {
            Dispose();
            return false;
        }
    }

    private bool ParseHeader()
    {
        var view = _view;
        if (view is null)
        {
            return false;
        }

        if (view.ReadUInt32(0) != Signature)
        {
            return false;
        }

        var capacity = view.Capacity;

        // Two header layouts exist in the wild: 8-byte aligned poll_time (offsets at 24) and
        // the packed variant (offsets at 20). Accept whichever produces consistent ranges.
        foreach (var baseOffset in new[] { 24, 20 })
        {
            var sensorOffset = view.ReadInt32(baseOffset);
            var sensorSize = view.ReadInt32(baseOffset + 4);
            var sensorCount = view.ReadInt32(baseOffset + 8);
            var readingOffset = view.ReadInt32(baseOffset + 12);
            var readingSize = view.ReadInt32(baseOffset + 16);
            var readingCount = view.ReadInt32(baseOffset + 20);

            if (sensorOffset <= 0 || sensorSize <= 0 || sensorCount <= 0 || sensorCount > 4096 ||
                readingOffset <= 0 || readingSize <= 0 || readingCount <= 0 || readingCount > 32768)
            {
                continue;
            }

            if ((long)sensorOffset + ((long)sensorSize * sensorCount) > capacity ||
                (long)readingOffset + ((long)readingSize * readingCount) > capacity)
            {
                continue;
            }

            _sensorOffset = sensorOffset;
            _sensorSize = sensorSize;
            _sensorCount = sensorCount;
            _readingOffset = readingOffset;
            _readingSize = readingSize;
            _readingCount = readingCount;
            _valueOffset = DetectValueOffset(view);
            return true;
        }

        return false;
    }

    private int DetectValueOffset(MemoryMappedViewAccessor view)
    {
        // Doubles sit at 284 in the packed element and at 288 when the SDK aligns them.
        foreach (var offset in new[] { 284, 288 })
        {
            for (var index = 0; index < Math.Min(_readingCount, 24); index++)
            {
                var position = _readingOffset + (index * _readingSize);
                try
                {
                    var type = view.ReadInt32(position);
                    var value = view.ReadDouble(position + offset);
                    if (type == 0 && double.IsFinite(value) && value > -40 && value < 200)
                    {
                        return offset;
                    }
                }
                catch (Exception)
                {
                    break;
                }
            }
        }

        return 284;
    }

    public double? ReadCpuTemperature()
    {
        var view = _view;
        if (view is null)
        {
            return null;
        }

        try
        {
            double? fallback = null;
            for (var index = 0; index < _readingCount; index++)
            {
                var position = _readingOffset + (index * _readingSize);
                var type = view.ReadInt32(position);
                if (type != 0)
                {
                    continue;
                }

                var readingId = view.ReadInt32(position + 8);
                var label = ReadAscii(view, position + 12, 128);
                var value = view.ReadDouble(position + _valueOffset);
                if (!double.IsFinite(value) || value <= -20 || value >= 150)
                {
                    continue;
                }

                if (readingId == CpuTemperatureReadingId)
                {
                    return value;
                }

                var sensorIndex = view.ReadInt32(position + 4);
                var sensorName = ReadSensorName(view, sensorIndex);
                var labelMatchesCpu = label.Contains("CPU", StringComparison.OrdinalIgnoreCase) ||
                                      label.Contains("Tctl", StringComparison.OrdinalIgnoreCase) ||
                                      label.Contains("Tdie", StringComparison.OrdinalIgnoreCase);
                var sensorMatchesCpu = sensorName.Contains("CPU", StringComparison.OrdinalIgnoreCase);

                if (labelMatchesCpu && (sensorMatchesCpu || sensorName.Length == 0))
                {
                    return value;
                }

                fallback ??= labelMatchesCpu || sensorMatchesCpu ? value : null;
            }

            return fallback;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private string ReadSensorName(MemoryMappedViewAccessor view, int sensorIndex)
    {
        if (sensorIndex < 0 || sensorIndex >= _sensorCount)
        {
            return string.Empty;
        }

        var position = _sensorOffset + (sensorIndex * _sensorSize);
        var name = ReadAscii(view, position + 8, 128);
        return name.Length == 0 ? ReadAscii(view, position, 128) : name;
    }

    private static string ReadAscii(MemoryMappedViewAccessor view, int position, int length)
    {
        var buffer = new byte[length];
        view.ReadArray(position, buffer, 0, length);
        var terminator = Array.IndexOf(buffer, (byte)0);
        if (terminator >= 0)
        {
            buffer = buffer[..terminator];
        }

        return Encoding.ASCII.GetString(buffer).Trim();
    }

    public void Dispose()
    {
        _view?.Dispose();
        _view = null;
        _file?.Dispose();
        _file = null;
    }
}

/// <summary>
/// Optional LibreHardwareMonitor bridge through its WMI provider. Requires the monitor to
/// run elevated with the WMI option enabled and Ring-0 drivers allowed (HVCI off).
/// </summary>
public sealed class LhmWmiSensorBridge : ISensorBridge
{
    private const string Scope = @"root\LibreHardwareMonitor";

    public string Name => "LibreHardwareMonitor";

    public bool IsAvailable { get; }

    public LhmWmiSensorBridge()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                Scope,
                "SELECT Identifier, Name, Value FROM Sensor WHERE SensorType='Temperature'");
            using var results = searcher.Get();
            var available = false;
            foreach (var item in results)
            {
                available = true;
                item.Dispose();
                break;
            }

            IsAvailable = available;
        }
        catch (Exception)
        {
            IsAvailable = false;
        }
    }

    public double? ReadCpuTemperature()
    {
        if (!IsAvailable)
        {
            return null;
        }

        try
        {
            using var searcher = new ManagementObjectSearcher(
                Scope,
                "SELECT Identifier, Name, Value FROM Sensor WHERE SensorType='Temperature'");
            using var results = searcher.Get();
            double? fallback = null;
            foreach (var item in results)
            {
                try
                {
                    var identifier = item["Identifier"] as string ?? string.Empty;
                    var name = item["Name"] as string ?? string.Empty;
                    var value = item["Value"] is null ? double.NaN : Convert.ToDouble(item["Value"]);
                    if (!double.IsFinite(value) || value <= -20 || value >= 150)
                    {
                        continue;
                    }

                    var isCpu = identifier.Contains("/amdcpu/", StringComparison.OrdinalIgnoreCase) ||
                                identifier.Contains("/intelcpu/", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("CPU", StringComparison.OrdinalIgnoreCase);
                    if (!isCpu)
                    {
                        continue;
                    }

                    var isCore = identifier.Contains("temperature/0", StringComparison.OrdinalIgnoreCase) ||
                                 name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                                 name.Contains("Tctl", StringComparison.OrdinalIgnoreCase);
                    if (isCore)
                    {
                        return value;
                    }

                    fallback ??= value;
                }
                finally
                {
                    item.Dispose();
                }
            }

            return fallback;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

public static class SensorBridgeDetector
{
    /// <summary>Prefers HWiNFO shared memory, then LibreHardwareMonitor WMI.</summary>
    public static ISensorBridge? Detect()
    {
        var hwinfo = new HwinfoSensorBridge();
        if (hwinfo.TryOpen())
        {
            return hwinfo;
        }

        hwinfo.Dispose();

        var lhm = new LhmWmiSensorBridge();
        if (lhm.IsAvailable)
        {
            return lhm;
        }

        return null;
    }
}
