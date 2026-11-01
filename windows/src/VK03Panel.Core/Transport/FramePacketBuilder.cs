using System.Text;

namespace VK03Panel.Core.Transport;

/// <summary>
/// MS912x bulk frame packaging: 8-byte header, BGR888 body with 0xFF replaced by 0xFE,
/// 8-byte tail. Mirrors the wire format used by the vendor SDK and the macOS application.
/// </summary>
public static class FramePacketBuilder
{
    public const byte ColorBgr888 = 0x11;
    public const int HeaderLength = 8;
    public const int TailLength = 8;
    public const int MaxChunkSize = 0x80000;

    private static readonly byte[] Tail = { 0xFF, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

    /// <summary>Wraps a BGR888 body into a complete panel frame packet.</summary>
    public static byte[] Build(ReadOnlySpan<byte> bgr, int width, int height)
    {
        var expected = width * height * 3;
        if (bgr.Length != expected)
        {
            throw new ArgumentException($"BGR 帧长度应为 {expected}，实际 {bgr.Length}。", nameof(bgr));
        }

        var packet = new byte[HeaderLength + expected + TailLength];
        packet[0] = 0xFF;
        packet[1] = 0x00;
        packet[2] = 0x00;
        packet[3] = 0x00;
        packet[4] = 0x00;
        packet[5] = (byte)(width >> 4);
        packet[6] = (byte)(((width & 0x0F) << 4) | ((height >> 8) & 0x0F));
        packet[7] = (byte)(height & 0xFF);

        for (var index = 0; index < expected; index++)
        {
            var value = bgr[index];
            packet[HeaderLength + index] = value == 0xFF ? (byte)0xFE : value;
        }

        Tail.CopyTo(packet, HeaderLength + expected);
        return packet;
    }

    public static string HeaderDescription(int width, int height)
    {
        var builder = new StringBuilder();
        builder.Append($"FF 00 00 00 00 {width >> 4:x2} {(((width & 0x0F) << 4) | ((height >> 8) & 0x0F)):x2} {height & 0xFF:x2}");
        return builder.ToString();
    }
}
