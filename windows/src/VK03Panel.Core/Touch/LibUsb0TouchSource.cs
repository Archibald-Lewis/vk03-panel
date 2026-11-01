using System.Runtime.InteropServices;
using VK03Panel.Core.Transport;
using VK03Panel.Core.Config;

namespace VK03Panel.Core.Touch;

/// <summary>
/// Reads the touch controller directly over libusb0 when the device is bound to the
/// libusb-win32 driver instead of the HID stack (mirrors the macOS raw-USB fallback).
/// </summary>
public sealed class LibUsb0TouchSource : ITouchSource
{
    private const ushort VendorId = 0x374A;
    private const ushort ProductId = 0xA401;
    private const int InterfaceNumber = 0;
    private const int EndpointIn = 0x81;
    // libusb-win32 error.h: ETRANSFER_TIMEDOUT (not the Linux errno value).
    private const int TransferTimedOut = -116;

    private readonly CancellationTokenSource _cancellation = new();
    private Thread? _thread;
    private long _reports;
    private volatile bool _connected;
    private string _lastError = string.Empty;

    public string Name => "USB 直读";

    public bool IsConnected => _connected;

    public long Reports => Interlocked.Read(ref _reports);

    public string LastError => _lastError;

    public event Action<byte[]>? ReportReceived;

    public void Start()
    {
        if (_thread is not null)
        {
            return;
        }

        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "VK03Panel.TouchUsb",
        };
        _thread.Start();
    }

    private void Run()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            IntPtr handle = IntPtr.Zero;
            var claimed = false;
            try
            {
                handle = FindAndOpen();
                if (handle == IntPtr.Zero)
                {
                    _lastError = "触屏未绑定到 libusb0";
                    _cancellation.Token.WaitHandle.WaitOne(2000);
                    continue;
                }

                var claim = LibUsb0Interop.usb_claim_interface(handle, InterfaceNumber);
                if (claim != 0)
                {
                    _lastError = $"触屏接口占用失败（{claim}）";
                    _cancellation.Token.WaitHandle.WaitOne(2000);
                    continue;
                }

                claimed = true;
                _connected = true;
                _lastError = string.Empty;
                Log.Info("触屏 USB 已连接（374a:a401，接口 0，端点 0x81）");

                var buffer = new byte[64];
                while (!_cancellation.IsCancellationRequested)
                {
                    var read = LibUsb0Interop.usb_interrupt_read(handle, EndpointIn, buffer, buffer.Length, 250);
                    if (read == TransferTimedOut)
                    {
                        // An idle touchscreen sends no packets. Keep the claimed handle
                        // open so the next press/release is not lost during reconnection.
                        continue;
                    }

                    if (read < 0)
                    {
                        _lastError = $"触屏读取中断（{read}，{LibUsb0Interop.LastErrorText().Trim()}）";
                        Log.Warn(_lastError);
                        break;
                    }

                    if (read == 0)
                    {
                        continue;
                    }

                    var report = TouchParser.Normalize(buffer.AsSpan(0, read));
                    var count = Interlocked.Increment(ref _reports);
                    if (count == 1)
                    {
                        Log.Info($"触屏已收到首个 USB 报告（{read} 字节）");
                    }
                    try
                    {
                        ReportReceived?.Invoke(report);
                    }
                    catch (Exception)
                    {
                        // Subscribers must not kill the reader.
                    }
                }
            }
            catch (Exception error)
            {
                _lastError = error.Message;
            }
            finally
            {
                _connected = false;
                if (handle != IntPtr.Zero)
                {
                    if (claimed)
                    {
                        LibUsb0Interop.usb_release_interface(handle, InterfaceNumber);
                    }

                    LibUsb0Interop.usb_close(handle);
                }
            }

            _cancellation.Token.WaitHandle.WaitOne(1500);
        }
    }

    private static IntPtr FindAndOpen()
    {
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
                        var handle = LibUsb0Interop.usb_open(ref device);
                        if (handle != IntPtr.Zero)
                        {
                            return handle;
                        }
                    }

                    devicePointer = device.Next;
                }

                busPointer = bus.Next;
            }
        }
        catch (DllNotFoundException)
        {
            // Driver missing.
        }
        catch (BadImageFormatException)
        {
            // Architecture mismatch.
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        _thread?.Join(TimeSpan.FromMilliseconds(1200));
        _thread = null;
        _connected = false;
    }
}
