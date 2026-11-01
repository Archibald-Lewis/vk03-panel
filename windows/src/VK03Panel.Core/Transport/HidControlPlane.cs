using System.Runtime.InteropServices;

namespace VK03Panel.Core.Transport;

/// <summary>
/// Panel control plane: 8-byte MS912x feature reports on the vendor HID interface.
/// Windows HID reports carry a leading report-ID byte, so the buffer length here is
/// FeatureReportByteLength (9 on the verified hardware) while the payload layout matches
/// the macOS implementation byte for byte. Both 8 and 9 byte report lengths are handled:
/// the data offset is always (reportLength - 5), i.e. 3 for 8-byte and 4 for 9-byte reports.
/// </summary>
public sealed class HidControlPlane : IDisposable
{
    private readonly object _sync = new();
    private IntPtr _handle = IntPtr.Zero;
    private int _reportLength;
    private int _dataOffset;

    public string DevicePath { get; }

    public string ProductName { get; }

    public ushort VendorId { get; }

    public ushort ProductId { get; }

    public ushort UsagePage { get; }

    public int ReportLength => _reportLength;

    private HidControlPlane(IntPtr handle, string path, string productName, ushort vendorId, ushort productId, ushort usagePage, int reportLength)
    {
        _handle = handle;
        DevicePath = path;
        ProductName = productName;
        VendorId = vendorId;
        ProductId = productId;
        UsagePage = usagePage;
        _reportLength = reportLength;
        _dataOffset = reportLength - 5;
    }

    /// <summary>Opens the first vendor HID interface matching the panel identifiers.</summary>
    public static HidControlPlane Open(ushort vendorId = 0x345F, ushort productId = 0x9132)
    {
        string? lastError = null;
        foreach (var path in HidInterop.EnumerateDevicePaths())
        {
            var handle = HidInterop.CreateFile(
                path,
                HidInterop.GenericRead | HidInterop.GenericWrite,
                HidInterop.FileShareRead | HidInterop.FileShareWrite,
                IntPtr.Zero,
                HidInterop.OpenExisting,
                0,
                IntPtr.Zero);

            if (handle == HidInterop.InvalidHandleValue)
            {
                lastError = $"无法打开 HID 设备：{Marshal.GetLastWin32Error()}";
                continue;
            }

            var attributes = new HidInterop.HidAttributes { Size = Marshal.SizeOf<HidInterop.HidAttributes>() };
            if (!HidInterop.HidD_GetAttributes(handle, ref attributes) ||
                attributes.VendorId != vendorId ||
                attributes.ProductId != productId)
            {
                HidInterop.CloseHandle(handle);
                continue;
            }

            if (!HidInterop.HidD_GetPreparsedData(handle, out var preparsed))
            {
                HidInterop.CloseHandle(handle);
                continue;
            }

            try
            {
                var caps = new HidInterop.HidpCaps();
                var result = HidInterop.HidP_GetCaps(preparsed, ref caps);
                if (result < 0 || caps.FeatureReportByteLength == 0)
                {
                    lastError = $"HID 功能报告不可用（HidP_GetCaps=0x{result:x}）";
                    continue;
                }

                return new HidControlPlane(
                    handle,
                    path,
                    HidInterop.ProductName(handle),
                    attributes.VendorId,
                    attributes.ProductId,
                    caps.UsagePage,
                    caps.FeatureReportByteLength);
            }
            finally
            {
                HidInterop.HidD_FreePreparsedData(preparsed);
            }
        }

        throw new PanelTransportException(
            "未找到副屏控制接口（345f:9132 的厂商 HID）" +
            (lastError is null ? "。" : $"：{lastError}。"));
    }

    /// <summary>Sends an 8-byte command body (the report ID byte is added automatically).</summary>
    public void Send(ReadOnlySpan<byte> payload)
    {
        lock (_sync)
        {
            var buffer = new byte[_reportLength];
            var count = Math.Min(payload.Length, _reportLength - 1);
            payload[..count].CopyTo(buffer.AsSpan(1));
            if (!HidInterop.HidD_SetFeature(_handle, buffer, buffer.Length))
            {
                throw new PanelTransportException($"HID 写入失败（Windows 错误 {Marshal.GetLastWin32Error()}）。");
            }
        }
    }

    /// <summary>Reads four xdata bytes starting at the given address.</summary>
    public byte[] ReadRegister(ushort address)
    {
        lock (_sync)
        {
            var command = new byte[_reportLength];
            command[1] = 0xB5;
            command[2] = (byte)(address >> 8);
            command[3] = (byte)(address & 0xFF);

            if (!HidInterop.HidD_SetFeature(_handle, command, command.Length))
            {
                throw new PanelTransportException($"HID 读取命令失败（地址 0x{address:x4}，Windows 错误 {Marshal.GetLastWin32Error()}）。");
            }

            var response = new byte[_reportLength];
            if (!HidInterop.HidD_GetFeature(_handle, response, response.Length))
            {
                throw new PanelTransportException($"HID 读取响应失败（地址 0x{address:x4}，Windows 错误 {Marshal.GetLastWin32Error()}）。");
            }

            return response.AsSpan(_dataOffset, 4).ToArray();
        }
    }

    public byte ReadByte(ushort address) => ReadRegister(address)[0];

    public void WriteRegister(ushort address, byte value)
    {
        Send(new byte[] { 0xB6, (byte)(address >> 8), (byte)(address & 0xFF), value });
    }

    /// <summary>Polls a status register until the first byte reads zero.</summary>
    public bool WaitStatus(ushort address, TimeSpan timeout, TimeSpan settle)
    {
        if (settle > TimeSpan.Zero)
        {
            Thread.Sleep(settle);
        }

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (ReadRegister(address)[0] == 0)
            {
                return true;
            }

            Thread.Sleep(4);
        }

        return false;
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            HidInterop.CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }
    }
}
