using System.IO;
using System.Management;
using VK03Panel.Core.Audio;
using VK03Panel.Core.Media;
using VK03Panel.Core.Metrics;
using VK03Panel.Core.Platform;
using VK03Panel.Core.Transport;

namespace VK03Panel.Core.Cli;

/// <summary>Prints a full diagnostics summary: devices, drivers, sensors, media and audio.</summary>
public static class DiagnoseCommand
{
    public static int Run(string[] args, TextWriter output)
    {
        output.WriteLine("VK03 Panel 诊断");
        output.WriteLine();

        output.WriteLine("[副屏与驱动]");
        output.WriteLine($"  libusb0：{(LibUsb0Transport.IsDriverAvailable() ? "可用" : "缺失")}");
        var usbDevices = LibUsb0Transport.Enumerate();
        output.WriteLine($"  libusb0 设备：{usbDevices.Count}");
        foreach (var device in usbDevices)
        {
            var mark = device.VendorId == 0x345F && device.ProductId == 0x9132 ? "  ← 副屏" : string.Empty;
            output.WriteLine($"    {device.VendorId:x4}:{device.ProductId:x4}  {device.Path}{mark}");
        }

        try
        {
            using var control = HidControlPlane.Open();
            output.WriteLine($"  控制 HID：{control.ProductName} · {control.ReportLength} 字节 · usage 0x{control.UsagePage:x4}");
        }
        catch (Exception error)
        {
            output.WriteLine($"  控制 HID：不可用（{error.Message}）");
        }

        WritePnpDevices(output, "%VID_345F%", "  345F 设备");
        WritePnpDevices(output, "%VID_374A%", "  374A 触屏");

        output.WriteLine();
        output.WriteLine("[传感器与整机]");
        using (var metrics = new MetricsAggregator())
        {
            metrics.Initialize();
            _ = metrics.Sample();
            Thread.Sleep(1100);
            var snapshot = metrics.Sample();
            foreach (var line in metrics.Describe().Split(Environment.NewLine))
            {
                output.WriteLine("  " + line);
            }

            output.WriteLine($"  实时采样   CPU {FormatPercent(snapshot.CpuLoad)} · {Format(snapshot.CpuFrequencyGhz, "0.00")} GHz · " +
                             $"{Format(snapshot.CpuTemperatureCelsius, "0")}°C · 内存 {Format(snapshot.MemoryUsedGb, "0.0")}/{Format(snapshot.MemoryTotalGb, "0")} GB · " +
                             $"GPU {FormatPercent(snapshot.GpuLoad)} · {Format(snapshot.GpuTemperatureCelsius, "0")}°C");
        }

        output.WriteLine();
        output.WriteLine("[媒体与音量]");
        using (var media = new MediaWatcher())
        {
            var started = Task.Run(() => media.InitializeAsync()).GetAwaiter().GetResult();
            Thread.Sleep(400);
            var state = media.Snapshot();
            output.WriteLine($"  GSMTC：{(started ? "可用" : "不可用")} · {(state.HasSession ? $"{state.Title} — {state.Artist}（{(state.Playing ? "播放中" : "已暂停")}）" : "无会话")}");
        }

        var audio = new EndpointVolume();
        output.WriteLine($"  音频端点：{audio.Describe()}");
        output.WriteLine($"  锁屏：{(LockScreen.IsAvailable ? "可用" : "不可用")}");
        output.WriteLine($"  开机自启：{(StartupRegistration.IsEnabled ? StartupRegistration.CurrentCommand : "未启用")}");

        return 0;
    }

    private static void WritePnpDevices(TextWriter output, string pattern, string title)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, PNPDeviceID, ConfigManagerErrorCode, Status FROM Win32_PnPEntity WHERE PNPDeviceID LIKE '" + pattern + "'");
            using var results = searcher.Get();
            var any = false;
            foreach (var item in results)
            {
                any = true;
                var name = item["Name"] as string ?? string.Empty;
                var id = item["PNPDeviceID"] as string ?? string.Empty;
                var code = item["ConfigManagerErrorCode"];
                var status = item["Status"] as string ?? string.Empty;
                var problem = code is null || Convert.ToInt32(code) == 0 ? "OK" : $"问题代码 {code}";
                output.WriteLine($"  {title}：{name} · {status} · {problem}");
                output.WriteLine($"    {id}");
                item.Dispose();
            }

            if (!any)
            {
                output.WriteLine($"  {title}：未找到");
            }
        }
        catch (Exception error)
        {
            output.WriteLine($"  {title}：查询失败（{error.Message}）");
        }
    }

    private static string Format(double? value, string format) =>
        value.HasValue ? value.Value.ToString(format, System.Globalization.CultureInfo.InvariantCulture) : "—";

    private static string FormatPercent(double? value) =>
        value.HasValue ? (value.Value * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%" : "—";
}
