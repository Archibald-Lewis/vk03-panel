using System.Runtime.InteropServices;

namespace VK03Panel.Core.Transport;

/// <summary>
/// Bulk video plane access through the libusb-win32 API. The VK03 panel exposes a
/// vendor video interface (number 3) with a bulk OUT endpoint 0x04 carrying MS912x
/// frames; interface 3 is the one the vendor driver binds to libusb0.
/// </summary>
public sealed class LibUsb0Transport : IDisposable
{
    private const int VendorId = 0x345F;
    private const int ProductId = 0x9132;
    private const int VideoInterface = 3;
    private const int BulkOutEndpoint = 0x04;
    private const int DefaultTimeout = 8000;

    private IntPtr _handle = IntPtr.Zero;
    private bool _claimed;

    public string DevicePath { get; private set; } = string.Empty;

    public bool IsOpen => _handle != IntPtr.Zero;

    /// <summary>Checks whether libusb0.dll can be loaded at all (driver installed).</summary>
    public static bool IsDriverAvailable()
    {
        try
        {
            LibUsb0Interop.usb_init();
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    /// <summary>Enumerates matching libusb0 devices without opening them.</summary>
    public static IReadOnlyList<(string Path, ushort VendorId, ushort ProductId)> Enumerate()
    {
        var results = new List<(string, ushort, ushort)>();
        try
        {
            LibUsb0Interop.usb_init();
            LibUsb0Interop.usb_find_busses();
            LibUsb0Interop.usb_find_devices();

            var busPointer = LibUsb0Interop.usb_get_busses();
            while (busPointer != IntPtr.Zero)
            {
                var bus = Marshal.PtrToStructure<LibUsb0Interop.UsbBus>(busPointer);
                var devicePointer = bus.Devices;
                while (devicePointer != IntPtr.Zero)
                {
                    var device = Marshal.PtrToStructure<LibUsb0Interop.UsbDevice>(devicePointer);
                    results.Add((device.FilenameText, device.Descriptor.VendorId, device.Descriptor.ProductId));
                    devicePointer = device.Next;
                }

                busPointer = bus.Next;
            }
        }
        catch (DllNotFoundException)
        {
            // Driver not present: enumeration simply yields nothing.
        }
        catch (BadImageFormatException)
        {
            // Architecture mismatch: same outcome.
        }

        return results;
    }

    public void Open()
    {
        if (IsOpen)
        {
            return;
        }

        try
        {
            LibUsb0Interop.usb_init();
            LibUsb0Interop.usb_find_busses();
            LibUsb0Interop.usb_find_devices();

            var busPointer = LibUsb0Interop.usb_get_busses();
            while (busPointer != IntPtr.Zero)
            {
                var bus = Marshal.PtrToStructure<LibUsb0Interop.UsbBus>(busPointer);
                var devicePointer = bus.Devices;
                while (devicePointer != IntPtr.Zero)
                {
                    var device = Marshal.PtrToStructure<LibUsb0Interop.UsbDevice>(devicePointer);
                    if (device.Descriptor.VendorId == VendorId && device.Descriptor.ProductId == ProductId)
                    {
                        DevicePath = device.FilenameText;
                        var handle = LibUsb0Interop.usb_open(ref device);
                        if (handle != IntPtr.Zero)
                        {
                            var claim = LibUsb0Interop.usb_claim_interface(handle, VideoInterface);
                            if (claim == 0)
                            {
                                _handle = handle;
                                _claimed = true;
                                LibUsb0Interop.usb_clear_halt(handle, BulkOutEndpoint);
                                return;
                            }

                            LibUsb0Interop.usb_close(handle);
                            throw new PanelTransportException(
                                $"无法占用副屏视频接口 {VideoInterface}（错误码 {claim}，{LibUsb0Interop.LastErrorText()}）。" +
                                "请退出正在使用副屏的厂商程序（如 Myth.Cool）后重试。");
                        }

                        throw new PanelTransportException(
                            $"无法打开副屏 USB 设备（{LibUsb0Interop.LastErrorText()}）。");
                    }

                    devicePointer = device.Next;
                }

                busPointer = bus.Next;
            }
        }
        catch (DllNotFoundException)
        {
            throw new PanelTransportException(
                "未找到 libusb0 驱动（libusb0.dll）。副屏视频传输需要厂商驱动；请先安装厂商程序或对应的 libusb-win32 驱动。");
        }
        catch (BadImageFormatException)
        {
            throw new PanelTransportException(
                "libusb0.dll 与当前进程架构不一致，请使用 64 位版本运行。");
        }

        throw new PanelTransportException(
            "未找到 VK03 副屏（345f:9132）。请确认副屏 USB 已连接，且设备管理器中的“MS USB Display”未被禁用。");
    }

    /// <summary>Writes a chunk to the bulk OUT endpoint and returns transferred bytes.</summary>
    public int Write(byte[] buffer, int offset, int count)
    {
        if (!IsOpen)
        {
            throw new PanelTransportException("副屏未连接。");
        }

        unsafe
        {
            fixed (byte* pointer = &buffer[offset])
            {
                return LibUsb0Interop.usb_bulk_write(_handle, BulkOutEndpoint, (IntPtr)pointer, count, DefaultTimeout);
            }
        }
    }

    /// <summary>Submits a zero-length packet terminating the current frame.</summary>
    public int WriteZeroLengthPacket()
    {
        if (!IsOpen)
        {
            throw new PanelTransportException("副屏未连接。");
        }

        return LibUsb0Interop.usb_bulk_write(_handle, BulkOutEndpoint, IntPtr.Zero, 0, 1000);
    }

    public void Close()
    {
        if (_handle == IntPtr.Zero)
        {
            return;
        }

        if (_claimed)
        {
            LibUsb0Interop.usb_release_interface(_handle, VideoInterface);
            _claimed = false;
        }

        LibUsb0Interop.usb_close(_handle);
        _handle = IntPtr.Zero;
    }

    public void Dispose()
    {
        try
        {
            Close();
        }
        catch (Exception)
        {
            // Closing a dead handle must never mask the original error.
        }
    }
}
