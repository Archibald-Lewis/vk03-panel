using VK03Panel.Core.Transport;
using Xunit;

namespace VK03Panel.Core.Tests;

public sealed class FramePacketBuilderTests
{
    [Fact]
    public void Build_WritesHeaderBodyAndTail()
    {
        var body = new byte[4 * 3 * 3];
        for (var index = 0; index < body.Length; index++)
        {
            body[index] = (byte)(index + 1);
        }

        body[0] = 0xFF;

        var packet = FramePacketBuilder.Build(body, 4, 3);

        Assert.Equal(FramePacketBuilder.HeaderLength + body.Length + FramePacketBuilder.TailLength, packet.Length);
        Assert.Equal(0xFF, packet[0]);
        Assert.Equal(0x00, packet[4]);
        Assert.Equal(0, packet[5]);      // 4 >> 4
        Assert.Equal(0x40, packet[6]);   // ((4 & 0xF) << 4) | ((3 >> 8) & 0xF) = 0x40
        Assert.Equal(3, packet[7]);
        Assert.Equal(0xFE, packet[8]);   // 0xFF is escaped in the body
        Assert.Equal(0x02, packet[9]);
        Assert.Equal(0xFF, packet[^8]);
        Assert.Equal(0xC0, packet[^7]);
    }

    [Fact]
    public void Build_EncodesTwelveBitDimensions()
    {
        var body = new byte[360 * 960 * 3];
        var packet = FramePacketBuilder.Build(body, 360, 960);
        Assert.Equal(360 >> 4, packet[5]);
        Assert.Equal(((360 & 0x0F) << 4) | ((960 >> 8) & 0x0F), packet[6]);
        Assert.Equal(960 & 0xFF, packet[7]);
    }

    [Fact]
    public void Build_RejectsMismatchedBody()
    {
        Assert.Throws<ArgumentException>(() => FramePacketBuilder.Build(new byte[10], 4, 3));
    }
}
