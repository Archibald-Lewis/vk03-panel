using System.Runtime.InteropServices;

namespace VK03Panel.Core.Transport;

/// <summary>
/// P/Invoke surface for libusb-win32 (libusb0.dll), the driver that the VK03 vendor
/// software installs for the MI_03 vendor interface. The struct layouts below are the
/// public libusb-win32 usb.h layouts (LIBUSB_PATH_MAX = 512), byte-for-byte compatible
/// with the 64-bit driver: pointers first, then the fixed path buffer, then the packed
/// 18-byte device descriptor.
/// </summary>
internal static class LibUsb0Interop
{
    internal const int LibusbPathMax = 512;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct UsbDeviceDescriptor
    {
        public byte Length;
        public byte DescriptorType;
        public ushort BcdUsb;
        public byte DeviceClass;
        public byte DeviceSubClass;
        public byte DeviceProtocol;
        public byte MaxPacketSize0;
        public ushort VendorId;
        public ushort ProductId;
        public ushort BcdDevice;
        public byte ManufacturerIndex;
        public byte ProductIndex;
        public byte SerialIndex;
        public byte ConfigurationCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct UsbBus
    {
        public IntPtr Next;
        public IntPtr Prev;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = LibusbPathMax)]
        public byte[] Dirname;

        public IntPtr Devices;
        public uint Location;      // unsigned long is 32-bit on Windows even for x64
        public IntPtr RootDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct UsbDevice
    {
        public IntPtr Next;
        public IntPtr Prev;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = LibusbPathMax)]
        public byte[] Filename;

        public IntPtr Bus;
        public UsbDeviceDescriptor Descriptor;

        // The packed descriptor is 18 bytes, so the next pointer needs 6 bytes of
        // padding to stay at the native offset (16 + 512 + 8 + 18 + 6 = 560).
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public byte[] Alignment;

        public IntPtr Config;
        public IntPtr Dev;
        public byte Devnum;
        public byte NumChildren;
        public IntPtr Children;

        internal string FilenameText
        {
            get
            {
                if (Filename is null)
                {
                    return string.Empty;
                }

                var length = Array.IndexOf(Filename, (byte)0);
                if (length < 0)
                {
                    length = Filename.Length;
                }

                return System.Text.Encoding.ASCII.GetString(Filename, 0, length);
            }
        }
    }

    [DllImport("libusb0.dll")]
    internal static extern void usb_init();

    [DllImport("libusb0.dll")]
    internal static extern int usb_find_busses();

    [DllImport("libusb0.dll")]
    internal static extern int usb_find_devices();

    [DllImport("libusb0.dll")]
    internal static extern IntPtr usb_get_busses();

    [DllImport("libusb0.dll")]
    internal static extern IntPtr usb_open(ref UsbDevice device);

    [DllImport("libusb0.dll")]
    internal static extern int usb_claim_interface(IntPtr device, int iface);

    [DllImport("libusb0.dll")]
    internal static extern int usb_release_interface(IntPtr device, int iface);

    [DllImport("libusb0.dll")]
    internal static extern int usb_clear_halt(IntPtr device, int endpoint);

    [DllImport("libusb0.dll")]
    internal static extern int usb_close(IntPtr device);

    [DllImport("libusb0.dll")]
    internal static extern int usb_bulk_write(IntPtr device, int endpoint, IntPtr data, int size, int timeout);

    [DllImport("libusb0.dll")]
    internal static extern int usb_interrupt_read(IntPtr device, int endpoint, byte[] data, int size, int timeout);

    [DllImport("libusb0.dll")]
    internal static extern IntPtr usb_strerror();

    internal static string LastErrorText()
    {
        try
        {
            var pointer = usb_strerror();
            return pointer == IntPtr.Zero ? "unknown libusb0 error" : Marshal.PtrToStringAnsi(pointer) ?? "unknown libusb0 error";
        }
        catch (Exception)
        {
            return "unknown libusb0 error";
        }
    }
}
