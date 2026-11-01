using System.IO;
using VK03Panel.Core.Rendering;
using VK03Panel.Core.Transport;

namespace VK03Panel.Core.Cli;

/// <summary>Hardware self-test: driver, device, chip, EDID, initialization and frame push.</summary>
public static class SelfTestCommand
{
    public static int Run(string[] args, TextWriter output)
    {
        var pushFrame = args.Contains("--frame", StringComparer.OrdinalIgnoreCase);
        var hold = ParseHold(args);
        var rotation = ParseRotation(args);

        output.WriteLine("VK03 Panel 自检");
        output.WriteLine($"  系统：{Environment.OSVersion.VersionString} · {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
        output.WriteLine($"  libusb0 驱动：{(LibUsb0Transport.IsDriverAvailable() ? "可用" : "缺失（libusb0.dll 无法加载）")}");

        var devices = LibUsb0Transport.Enumerate();
        output.WriteLine($"  libusb0 设备：{devices.Count} 个");
        foreach (var device in devices)
        {
            var match = device.VendorId == 0x345F && device.ProductId == 0x9132 ? "  ← 副屏" : string.Empty;
            output.WriteLine($"    {device.VendorId:x4}:{device.ProductId:x4}  {device.Path}{match}");
        }

        if (!devices.Any(device => device.VendorId == 0x345F && device.ProductId == 0x9132))
        {
            output.WriteLine("结果：未找到副屏，停止。");
            return 2;
        }

        using var transport = new PanelTransport();
        try
        {
            transport.Connect(message => output.WriteLine($"  {message}"));

            var info = transport.Info;
            output.WriteLine("  设备摘要：");
            output.WriteLine($"    型号      {info.PanelModel}");
            output.WriteLine($"    分辨率    {info.PanelWidth}×{info.PanelHeight}");
            output.WriteLine($"    芯片      {info.ChipFamily}（{info.ChipSignature}）");
            output.WriteLine($"    视频端口  {info.VideoPort}");
            output.WriteLine($"    控制 HID  {info.HidProductName} · 报告 {info.HidReportLength} 字节");
            output.WriteLine($"    USB       {info.UsbPath}");

            output.WriteLine("  寄存器快照：");
            foreach (var address in new ushort[] { 0xF000, 0x0030, 0x0031, 0x0032, 0xC454, 0xC555, 0xC557, 0xC558, 0xF507 })
            {
                var value = transport.ReadRegister(address);
                output.WriteLine($"    0x{address:x4}  {BitConverter.ToString(value)}");
            }

            if (!pushFrame)
            {
                output.WriteLine("结果：连接与校验通过（未推帧，使用 --frame 进行实际点亮测试）。");
                return 0;
            }

            var bgra = TestPattern.CreateLandscapeBgra();
            var bgr = BgrFrameEncoder.Encode(bgra, TestPattern.LandscapeWidth, TestPattern.LandscapeHeight, rotation);
            var timings = new List<double>();

            for (var frame = 0; frame < 5; frame++)
            {
                transport.SendFrame(bgr);
                timings.Add(transport.LastFrameMilliseconds);
                output.WriteLine($"    帧 {frame + 1}/5：{transport.LastFrameMilliseconds:F1} ms");
                Thread.Sleep(400);
            }

            output.WriteLine($"  零长度包：{(transport.ZeroLengthPacketAccepted ? "已接受" : "被驱动忽略（短包终止）")}");
            output.WriteLine($"  平均帧耗时：{timings.Average():F1} ms（{transport.FramesSent} 帧已发送）");

            if (hold > 0)
            {
                output.WriteLine($"  屏幕应显示色带测试图（方向 {rotation}°），保持 {hold} 秒……");
                Thread.Sleep(TimeSpan.FromSeconds(hold));
            }

            output.WriteLine("结果：副屏已成功点亮并接收帧。");
            return 0;
        }
        catch (PanelTransportException error)
        {
            output.WriteLine($"结果：失败 — {error.Message}");
            return 2;
        }
        catch (Exception error)
        {
            output.WriteLine($"结果：异常 — {error.GetType().Name}: {error.Message}");
            return 2;
        }
    }

    private static int ParseHold(string[] args)
    {
        var index = Array.FindIndex(args, argument => argument.Equals("--hold", StringComparison.OrdinalIgnoreCase));
        if (index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out var seconds))
        {
            return Math.Clamp(seconds, 0, 120);
        }

        return 0;
    }

    private static int ParseRotation(string[] args)
    {
        var index = Array.FindIndex(args, argument => argument.Equals("--rotation", StringComparison.OrdinalIgnoreCase));
        if (index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out var rotation) &&
            BgrFrameEncoder.IsSupportedRotation(rotation))
        {
            return rotation;
        }

        return 90;
    }
}
