using System.Runtime.InteropServices;
using VK03Panel.Core.Transport;

namespace VK03Panel.Core.Touch;

/// <summary>Touch input backend for the panel's PUYA controller (374a:a401).</summary>
public interface ITouchSource : IDisposable
{
    string Name { get; }

    bool IsConnected { get; }

    long Reports { get; }

    string LastError { get; }

    event Action<byte[]>? ReportReceived;

    void Start();
}

/// <summary>
/// Reads the touch controller as a Windows HID device. This is the preferred path: the
/// controller uses a vendor-defined HID descriptor, so Windows does not treat it as a desktop
/// digitizer and reports can be consumed without moving the cursor.
/// </summary>
public sealed class HidTouchSource : ITouchSource
{
    private const ushort VendorId = 0x374A;
    private const ushort ProductId = 0xA401;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileFlagOverlapped = 0x40000000;
    private const int ErrorIoPending = 997;
    private const uint WaitObject0 = 0;
    private const uint WaitTimeout = 258;

    private static readonly IntPtr InvalidHandle = new(-1);

    private readonly CancellationTokenSource _cancellation = new();
    private Thread? _thread;
    private IntPtr _handle = IntPtr.Zero;
    private string _lastError = string.Empty;
    private long _reports;
    private volatile bool _connected;

    public string Name => "HID";

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
            Name = "VK03Panel.TouchHid",
        };
        _thread.Start();
    }

    private void Run()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            try
            {
                var path = FindDevicePath();
                if (path is null)
                {
                    _lastError = "未找到触屏 HID 设备（374a:a401）";
                    _cancellation.Token.WaitHandle.WaitOne(2000);
                    continue;
                }

                _handle = CreateFile(path, GenericRead | GenericWrite, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, FileFlagOverlapped, IntPtr.Zero);
                if (_handle == InvalidHandle)
                {
                    _lastError = $"无法打开触屏 HID：Win32 {Marshal.GetLastWin32Error()}";
                    _cancellation.Token.WaitHandle.WaitOne(2000);
                    continue;
                }

                _connected = true;
                _lastError = string.Empty;
                ReadLoop();
            }
            catch (Exception error)
            {
                _lastError = error.Message;
            }
            finally
            {
                _connected = false;
                if (_handle != IntPtr.Zero)
                {
                    CloseHandle(_handle);
                    _handle = IntPtr.Zero;
                }
            }

            _cancellation.Token.WaitHandle.WaitOne(1000);
        }
    }

    private void ReadLoop()
    {
        var buffer = new byte[64];
        var readEvent = CreateEvent(IntPtr.Zero, true, false, null);
        if (readEvent == IntPtr.Zero)
        {
            _lastError = "无法创建触屏读取事件";
            return;
        }

        try
        {
            var overlapped = new NativeOverlapped { EventHandle = readEvent };
            while (!_cancellation.IsCancellationRequested)
            {
                ResetEvent(readEvent);
                var pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try
                {
                    var success = ReadFile(_handle, pinned.AddrOfPinnedObject(), (uint)buffer.Length, out var transferred, ref overlapped);
                    if (!success)
                    {
                        var error = Marshal.GetLastWin32Error();
                        if (error != ErrorIoPending)
                        {
                            _lastError = $"触屏读取失败：Win32 {error}";
                            return;
                        }

                        var wait = WaitForSingleObject(readEvent, 250);
                        if (wait == WaitTimeout)
                        {
                            CancelIoEx(_handle, ref overlapped);
                            WaitForSingleObject(readEvent, 100);
                            continue;
                        }

                        if (wait != WaitObject0 || !GetOverlappedResult(_handle, ref overlapped, out transferred, false))
                        {
                            _lastError = "触屏读取中断";
                            return;
                        }
                    }

                    if (transferred == 0)
                    {
                        continue;
                    }

                    var report = TouchParser.Normalize(buffer.AsSpan(0, (int)transferred));
                    Interlocked.Increment(ref _reports);
                    try
                    {
                        ReportReceived?.Invoke(report);
                    }
                    catch (Exception)
                    {
                        // Subscribers must not kill the reader.
                    }
                }
                finally
                {
                    pinned.Free();
                }
            }
        }
        finally
        {
            CloseHandle(readEvent);
        }
    }

    private static string? FindDevicePath()
    {
        foreach (var path in HidInterop.EnumerateDevicePaths())
        {
            var handle = CreateFile(path, GenericRead | GenericWrite, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (handle == InvalidHandle)
            {
                continue;
            }

            try
            {
                var attributes = new HidInterop.HidAttributes { Size = Marshal.SizeOf<HidInterop.HidAttributes>() };
                if (HidInterop.HidD_GetAttributes(handle, ref attributes) &&
                    attributes.VendorId == VendorId &&
                    attributes.ProductId == ProductId)
                {
                    return path;
                }
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        return null;
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        if (_handle != IntPtr.Zero)
        {
            CancelIoEx(_handle, IntPtr.Zero);
        }

        _thread?.Join(TimeSpan.FromMilliseconds(1200));
        _thread = null;
        _connected = false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeOverlapped
    {
        public IntPtr Internal;
        public IntPtr InternalHigh;
        public int Offset;
        public int OffsetHigh;
        public IntPtr EventHandle;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFile(string fileName, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadFile(IntPtr handle, IntPtr buffer, uint length, out uint transferred, ref NativeOverlapped overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOverlappedResult(IntPtr handle, ref NativeOverlapped overlapped, out uint transferred, bool wait);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CancelIoEx(IntPtr handle, ref NativeOverlapped overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CancelIoEx(IntPtr handle, IntPtr overlapped);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateEvent(IntPtr attributes, [MarshalAs(UnmanagedType.Bool)] bool manualReset, [MarshalAs(UnmanagedType.Bool)] bool initialState, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ResetEvent(IntPtr handle);
}
