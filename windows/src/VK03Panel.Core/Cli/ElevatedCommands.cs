using System.IO;
using System.Text.Json;
using VK03Panel.Core.Config;
using VK03Panel.Core.Metrics;
using VK03Panel.Core.Platform;

namespace VK03Panel.Core.Cli;

/// <summary>Elevated diagnostics: AMD sensor dump and touch device repair.</summary>
public static class ElevatedCommands
{
    public static int Run(string[] args, TextWriter output)
    {
        if (args.Contains("--amd-dump", StringComparer.OrdinalIgnoreCase))
        {
            return AmdDump(output);
        }

        if (args.Contains("--repair-touch", StringComparer.OrdinalIgnoreCase))
        {
            return RepairTouch(output);
        }

        if (args.Contains("--touch-status", StringComparer.OrdinalIgnoreCase))
        {
            var status = TouchDeviceRepair.Query();
            output.WriteLine(status is null
                ? "触屏：未找到（374a:a401）"
                : $"触屏：{status.Name} · {status.Status} · 问题代码 {status.ProblemCode} · {status.InstanceId}");
            return 0;
        }

        output.WriteLine("用法：--amd-dump | --repair-touch | --touch-status");
        return 1;
    }

    private static int AmdDump(TextWriter output)
    {
        using var bridge = new AmdSdkSensorBridge();
        bridge.Initialize();
        output.WriteLine($"AMD SDK：{bridge.Describe()}");

        var reading = bridge.Read();
        if (reading is null)
        {
            output.WriteLine($"读取失败：{bridge.LastError}");
            return 2;
        }

        var report = new
        {
            timestamp = reading.Timestamp,
            temperature = reading.TemperatureCelsius,
            temperatureOffset = reading.TemperatureOffset,
            library = bridge.LibraryPath,
            doubles = reading.Doubles.Select(value => new { offset = $"0x{value.Offset:x}", value = value.Value }),
            floats = reading.Floats.Select(value => new { offset = $"0x{value.Offset:x}", value = value.Value }),
        };

        var path = AppPaths.DiagnosticsFile("amd-sdk.json");
        Directory.CreateDirectory(AppPaths.DataDirectory);
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

        output.WriteLine($"已写入 {path}");
        output.WriteLine($"温度：{(reading.TemperatureCelsius.HasValue ? reading.TemperatureCelsius.Value.ToString("0.0") + " °C" : "未找到")}" +
                         $"（偏移 {(reading.TemperatureOffset.HasValue ? $"0x{reading.TemperatureOffset.Value:x}" : "—")}）");
        foreach (var value in reading.Doubles)
        {
            output.WriteLine($"  double 0x{value.Offset:x} = {value.Value:0.####}");
        }

        return 0;
    }

    private static int RepairTouch(TextWriter output)
    {
        output.WriteLine("触屏修复：需要管理员权限");
        var result = TouchDeviceRepair.Repair(message => output.WriteLine("  " + message));
        TouchDeviceRepair.WriteResult(result);
        output.WriteLine(result.Success ? "结果：设备已启动" : $"结果：失败（{result.Error}）");
        output.WriteLine($"详情：{TouchDeviceRepair.ResultPath}");
        return result.Success ? 0 : 2;
    }
}
