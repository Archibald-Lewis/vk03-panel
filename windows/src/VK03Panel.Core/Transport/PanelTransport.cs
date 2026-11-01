using System.Diagnostics;
using System.Text;

namespace VK03Panel.Core.Transport;

/// <summary>
/// Owns the VK03 panel: validates the hardware, runs the MS912x initialization sequence,
/// and streams BGR frames over the libusb0 bulk endpoint while toggling the screen gate and
/// video output exactly once per connection.
/// </summary>
public sealed class PanelTransport : IDisposable
{
    public const int PanelWidth = 360;
    public const int PanelHeight = 960;
    public const int FrameByteCount = PanelWidth * PanelHeight * 3;

    private const byte ExpectedChip0 = 0xB7;
    private const byte ExpectedChip1 = 0x16;
    private const byte ExpectedChip2 = 0x0A;
    private const string ExpectedModel = "MCVKC03";

    private static readonly ushort StatusTransferMode = 0xC558;
    private static readonly ushort StatusPower = 0xC454;
    private static readonly ushort StatusVideoIn = 0xC555;
    private static readonly ushort StatusVideoOut = 0xC557;

    private readonly object _sync = new();
    private LibUsb0Transport? _usb;
    private HidControlPlane? _control;
    private byte _frameId = 1;
    private bool _videoEnabled;
    private bool _zeroLengthPacketAccepted = true;

    public PanelDeviceInfo Info { get; private set; } = PanelDeviceInfo.Empty;

    public bool IsConnected => _usb is { IsOpen: true } && _control is not null;

    public long FramesSent { get; private set; }

    public double LastFrameMilliseconds { get; private set; }

    public bool ZeroLengthPacketAccepted => _zeroLengthPacketAccepted;

    /// <summary>Connects, validates the panel and runs the initialization sequence.</summary>
    public void Connect(Action<string>? log = null)
    {
        lock (_sync)
        {
            if (IsConnected)
            {
                return;
            }

            DisposeHandles();

            try
            {
                _usb = new LibUsb0Transport();
                _usb.Open();
                log?.Invoke($"USB 视频接口已占用：{_usb.DevicePath}");

                _control = HidControlPlane.Open();
                log?.Invoke($"HID 控制接口：{_control.ProductName}（报告 {_control.ReportLength} 字节）");

                Info = Validate();
                log?.Invoke($"面板校验通过：{Info.Summary}");

                Initialize(intermediate: true);
                log?.Invoke("初始化完成，等待首帧。");
            }
            catch (Exception)
            {
                DisposeHandles();
                throw;
            }
        }
    }

    private PanelDeviceInfo Validate()
    {
        var control = RequireControl();

        var chip = control.ReadRegister(0xF000);
        var chipSignature = $"{chip[0]:X2} {chip[1]:X2} {chip[2]:X2}";
        if (chip[0] != ExpectedChip0 || chip[1] != ExpectedChip1 || chip[2] != ExpectedChip2)
        {
            throw new PanelTransportException(
                $"芯片签名 {chipSignature} 与 MS912C 不符，已停止初始化以避免误刷其他小屏。");
        }

        var sdram = control.ReadRegister(0x0030);
        var port = sdram[1];
        if (port is not (5 or 6))
        {
            throw new PanelTransportException($"未知的视频端口 {port}，仅支持端口 5 与 6。");
        }

        var edid = ReadEdid(control);
        if (edid.Length != 128 || edid[0] != 0x00 || edid[1] != 0xFF || edid[7] != 0x00)
        {
            throw new PanelTransportException("副屏 EDID 头无效，已停止初始化。");
        }

        var checksum = edid.Aggregate(0, (current, value) => current + value) & 0xFF;
        if (checksum != 0)
        {
            throw new PanelTransportException($"副屏 EDID 校验和无效（{checksum}），已停止初始化。");
        }

        var width = edid[56] + ((edid[58] & 0xF0) << 4);
        var height = edid[59] + ((edid[61] & 0xF0) << 4);
        if (width != PanelWidth || height != PanelHeight)
        {
            throw new PanelTransportException($"面板分辨率 {width}×{height} 不受支持，需要 360×960。");
        }

        var model = ReadEdidModel(edid);
        if (!string.Equals(model, ExpectedModel, StringComparison.Ordinal))
        {
            throw new PanelTransportException($"面板型号“{model}”不受支持，需要 {ExpectedModel}。");
        }

        return new PanelDeviceInfo
        {
            UsbPath = _usb?.DevicePath ?? string.Empty,
            HidPath = control.DevicePath,
            HidProductName = control.ProductName,
            HidReportLength = control.ReportLength,
            VendorId = control.VendorId,
            ProductId = control.ProductId,
            ChipSignature = chipSignature,
            ChipFamily = "MS912C",
            VideoPort = port,
            PanelModel = model,
            PanelWidth = width,
            PanelHeight = height,
            EdidValid = true,
        };
    }

    private static byte[] ReadEdid(HidControlPlane control)
    {
        var edid = new byte[128];
        for (var offset = 0; offset < 128; offset += 4)
        {
            var block = control.ReadRegister((ushort)(0xC000 + offset));
            Array.Copy(block, 0, edid, offset, Math.Min(4, 128 - offset));
        }

        return edid;
    }

    private static string ReadEdidModel(byte[] edid)
    {
        var builder = new StringBuilder();
        for (var offset = 54; offset + 17 < 126; offset += 18)
        {
            if (edid[offset + 3] != 0xFC)
            {
                continue;
            }

            builder.Clear();
            for (var index = 5; index < 18; index++)
            {
                var value = edid[offset + index];
                if (value is 0x00 or 0x0A or 0x0D)
                {
                    break;
                }

                builder.Append((char)value);
            }
        }

        return builder.ToString().Trim();
    }

    private void Initialize(bool intermediate)
    {
        var control = RequireControl();
        Command(control, new byte[] { 0xA6, 0x03, 0x03 }, StatusTransferMode, TimeSpan.FromMilliseconds(20));
        control.WriteRegister(0xDEEE, 0x01);
        Command(control, new byte[] { 0xA6, 0x07, 0x01 }, StatusPower, TimeSpan.FromMilliseconds(120));
        Command(control, new byte[] { 0xA6, 0x05, 0x00 }, StatusVideoIn, TimeSpan.FromMilliseconds(20));
        SetScreen(false);

        if (intermediate)
        {
            // The vendor SDK applies a 1080p intermediate state before the real mode.
            // It is harmless for 0x11 and keeps the sequence identical to the macOS app.
            Command(
                control,
                new byte[] { 0xA6, 0x01, 0x07, 0x80, 0x04, 0x38, 0x22, 0x00 },
                StatusVideoIn,
                TimeSpan.FromMilliseconds(20));
            Command(
                control,
                new byte[] { 0xA6, 0x02, 0x81, 0x00, 0x07, 0x80, 0x04, 0x38 },
                StatusVideoOut,
                TimeSpan.FromMilliseconds(20));
            Thread.Sleep(50);
        }

        Command(
            control,
            new byte[]
            {
                0xA6, 0x01,
                (byte)(PanelWidth >> 8), (byte)(PanelWidth & 0xFF),
                (byte)(PanelHeight >> 8), (byte)(PanelHeight & 0xFF),
                FramePacketBuilder.ColorBgr888, 0x00,
            },
            StatusVideoIn,
            TimeSpan.FromMilliseconds(20));

        Command(
            control,
            new byte[]
            {
                0xA6, 0x02,
                0xA0, 0x00,
                (byte)(PanelWidth >> 8), (byte)(PanelWidth & 0xFF),
                (byte)(PanelHeight >> 8), (byte)(PanelHeight & 0xFF),
            },
            StatusVideoOut,
            TimeSpan.FromMilliseconds(20));

        Command(control, new byte[] { 0xA6, 0x04, 0x01 }, StatusVideoIn, TimeSpan.FromMilliseconds(20));
        _videoEnabled = false;
    }

    private static void Command(HidControlPlane control, byte[] payload, ushort statusAddress, TimeSpan settle)
    {
        control.Send(payload);
        if (!control.WaitStatus(statusAddress, TimeSpan.FromSeconds(1.5), settle))
        {
            throw new PanelTransportException($"面板命令 0x{payload[1]:X2} 超时（状态地址 0x{statusAddress:X4}）。");
        }
    }

    /// <summary>Streams one complete BGR888 frame and triggers it on the panel.</summary>
    public void SendFrame(byte[] bgr)
    {
        if (bgr.Length != FrameByteCount)
        {
            throw new ArgumentException($"帧长度应为 {FrameByteCount}，实际 {bgr.Length}。", nameof(bgr));
        }

        lock (_sync)
        {
            if (!IsConnected)
            {
                throw new PanelTransportException("副屏未连接。");
            }

            var started = Stopwatch.GetTimestamp();
            var packet = FramePacketBuilder.Build(bgr, PanelWidth, PanelHeight);
            WriteChunks(RequireUsb(), packet);

            var zeroLength = RequireUsb().WriteZeroLengthPacket();
            if (zeroLength < 0)
            {
                // The frame is not a multiple of the endpoint packet size, so the final short
                // packet already terminates it. Keep the connection instead of failing.
                _zeroLengthPacketAccepted = false;
            }

            var control = RequireControl();
            control.Send(new byte[] { 0xA6, 0x00, _frameId, 100, 0x00, 0x00, 0x00, 0x00 });
            _frameId ^= 1;

            if (!_videoEnabled)
            {
                SetScreen(true);
                Command(control, new byte[] { 0xA6, 0x05, 0x01 }, StatusVideoIn, TimeSpan.FromMilliseconds(20));
                _videoEnabled = true;
            }

            FramesSent++;
            LastFrameMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }
    }

    private static void WriteChunks(LibUsb0Transport usb, byte[] packet)
    {
        var offset = 0;
        while (offset < packet.Length)
        {
            var chunk = Math.Min(FramePacketBuilder.MaxChunkSize, packet.Length - offset);
            var written = usb.Write(packet, offset, chunk);
            if (written <= 0)
            {
                throw new PanelTransportException(
                    $"USB 帧写入失败（偏移 {offset}，返回 {written}，{LibUsb0Interop.LastErrorText()}）。");
            }

            offset += written;
        }
    }

    /// <summary>Turns the panel backlight gate off (used when the app exits).</summary>
    public void TurnOff()
    {
        lock (_sync)
        {
            if (_control is null)
            {
                return;
            }

            try
            {
                SetScreen(false);
                _videoEnabled = false;
            }
            catch (Exception)
            {
                // Turning the screen off is best effort during shutdown.
            }
        }
    }

    private void SetScreen(bool on)
    {
        var control = RequireControl();
        var address = Info.VideoPort == 5 ? (ushort)0xF507 : (ushort)0xF005;
        var mask = Info.VideoPort == 5 ? (byte)0x02 : (byte)0x10;
        var inverted = Info.VideoPort == 5;

        var current = control.ReadByte(address);
        var setBit = inverted ? !on : on;
        var updated = setBit ? (byte)(current | mask) : (byte)(current & ~mask);
        if (updated != current)
        {
            control.WriteRegister(address, updated);
        }
    }

    /// <summary>Diagnostic read of four xdata bytes at an address.</summary>
    public byte[] ReadRegister(ushort address) => RequireControl().ReadRegister(address);

    private LibUsb0Transport RequireUsb() =>
        _usb ?? throw new PanelTransportException("副屏 USB 通道未建立。");

    private HidControlPlane RequireControl() =>
        _control ?? throw new PanelTransportException("副屏控制通道未建立。");

    private void DisposeHandles()
    {
        try
        {
            _control?.Dispose();
        }
        catch (Exception)
        {
            // Ignore teardown errors.
        }

        _control = null;

        try
        {
            _usb?.Dispose();
        }
        catch (Exception)
        {
            // Ignore teardown errors.
        }

        _usb = null;
        _videoEnabled = false;
    }

    /// <summary>Releases the device, optionally turning the screen gate off first.</summary>
    public void Shutdown(bool turnScreenOff)
    {
        lock (_sync)
        {
            if (turnScreenOff)
            {
                TurnOff();
            }

            DisposeHandles();
        }
    }

    public void Dispose()
    {
        Shutdown(turnScreenOff: true);
    }
}
