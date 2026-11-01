using System.IO.MemoryMappedFiles;
using System.Text;
using VK03Panel.Core.Metrics;
using Xunit;

namespace VK03Panel.Core.Tests;

public sealed class HwinfoSensorBridgeTests
{
    private const int HeaderSize = 48;
    private const int SensorSize = 264;
    private const int ReadingSize = 316;

    [Fact]
    public void ReadsCpuTemperatureFromSyntheticSharedMemory()
    {
        var name = @"Local\VK03Panel.Tests.HWiNFO";
        using var mapping = MemoryMappedFile.CreateNew(name, 1024, MemoryMappedFileAccess.ReadWrite);
        using (var view = mapping.CreateViewAccessor(0, 1024, MemoryMappedFileAccess.Write))
        {
            WriteHeader(view, sensorOffset: HeaderSize, sensorCount: 1, readingOffset: HeaderSize + SensorSize, readingCount: 1);

            var sensor = HeaderSize;
            view.Write(sensor, 1000);              // sensor id
            view.Write(sensor + 4, 0);             // instance
            WriteAscii(view, sensor + 8, "CPU [#0]: AMD Ryzen 7 9700X");

            var reading = HeaderSize + SensorSize;
            view.Write(reading, 0);                          // type: temperature
            view.Write(reading + 4, 0);                      // sensor index
            view.Write(reading + 8, 0x00010000);             // CPU temperature reading id
            WriteAscii(view, reading + 12, "CPU Package");
            WriteAscii(view, reading + 268, "°C");
            view.Write(reading + 284, 48.5d);
        }

        using var bridge = new HwinfoSensorBridge(name);
        Assert.True(bridge.TryOpen());
        var temperature = bridge.ReadCpuTemperature();
        Assert.NotNull(temperature);
        Assert.Equal(48.5, temperature!.Value, 3);
    }

    [Fact]
    public void IgnoresMappingWithWrongSignature()
    {
        var name = @"Local\VK03Panel.Tests.BadHWiNFO";
        using var mapping = MemoryMappedFile.CreateNew(name, 1024, MemoryMappedFileAccess.ReadWrite);
        using (var view = mapping.CreateViewAccessor(0, 1024, MemoryMappedFileAccess.Write))
        {
            view.Write(0, 0x12345678);
        }

        using var bridge = new HwinfoSensorBridge(name);
        Assert.False(bridge.TryOpen());
    }

    private static void WriteHeader(MemoryMappedViewAccessor view, int sensorOffset, int sensorCount, int readingOffset, int readingCount)
    {
        view.Write(0, 0x53695748);     // 'HWiS'
        view.Write(4, 2);              // version
        view.Write(8, 0);              // revision
        view.Write(16, 0L);            // poll time
        view.Write(24, sensorOffset);
        view.Write(28, SensorSize);
        view.Write(32, sensorCount);
        view.Write(36, readingOffset);
        view.Write(40, ReadingSize);
        view.Write(44, readingCount);
    }

    private static void WriteAscii(MemoryMappedViewAccessor view, int offset, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        view.WriteArray(offset, bytes, 0, Math.Min(bytes.Length, 120));
        view.Write(offset + bytes.Length, (byte)0);
    }
}
