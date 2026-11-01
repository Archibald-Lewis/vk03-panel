using VK03Panel.Core.Rendering;
using Xunit;

namespace VK03Panel.Core.Tests;

public sealed class BgrFrameEncoderTests
{
    private static byte[] CreateLandscapeBgra()
    {
        var bgra = new byte[960 * 360 * 4];
        Paint(bgra, 960, 0, 0, 0, 0, 255);          // top-left: red
        Paint(bgra, 960, 959, 0, 0, 255, 0);        // top-right: green
        Paint(bgra, 960, 0, 359, 255, 0, 0);        // bottom-left: blue
        Paint(bgra, 960, 959, 359, 255, 255, 255);  // bottom-right: white
        return bgra;
    }

    private static void Paint(byte[] bgra, int width, int x, int y, byte b, byte g, byte r)
    {
        var index = ((y * width) + x) * 4;
        bgra[index] = b;
        bgra[index + 1] = g;
        bgra[index + 2] = r;
        bgra[index + 3] = 0xFF;
    }

    private static (byte B, byte G, byte R) Read(byte[] bgr, int x, int y)
    {
        var index = ((y * 360) + x) * 3;
        return (bgr[index], bgr[index + 1], bgr[index + 2]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void Encode_ProducesPortraitBuffer(int rotation)
    {
        var (width, height) = BgrFrameEncoder.SourceSize(rotation);
        var source = new byte[width * height * 4];
        var bgr = BgrFrameEncoder.Encode(source, width, height, rotation);
        Assert.Equal(360 * 960 * 3, bgr.Length);
    }

    [Fact]
    public void Encode_Rotation90_MapsLandscapeCorners()
    {
        var bgr = BgrFrameEncoder.Encode(CreateLandscapeBgra(), 960, 360, 90);

        // dest(x, y) = source(y, 359 - x)
        Assert.Equal((byte)0, Read(bgr, 359, 0).B);
        Assert.Equal((byte)0, Read(bgr, 359, 0).G);
        Assert.Equal((byte)255, Read(bgr, 359, 0).R);   // source top-left red

        Assert.Equal((byte)255, Read(bgr, 0, 0).B);
        Assert.Equal((byte)0, Read(bgr, 0, 0).G);
        Assert.Equal((byte)0, Read(bgr, 0, 0).R);       // source bottom-left blue

        Assert.Equal((byte)0, Read(bgr, 359, 959).B);
        Assert.Equal((byte)255, Read(bgr, 359, 959).G); // source top-right green

        Assert.Equal((byte)255, Read(bgr, 0, 959).B);   // source bottom-right white
        Assert.Equal((byte)255, Read(bgr, 0, 959).G);
        Assert.Equal((byte)255, Read(bgr, 0, 959).R);
    }

    [Fact]
    public void Encode_Rotation180_UsesPortraitSource()
    {
        var portrait = new byte[360 * 960 * 4];
        Paint(portrait, 360, 0, 0, 1, 2, 3);
        var bgr = BgrFrameEncoder.Encode(portrait, 360, 960, 180);
        Assert.Equal((byte)1, Read(bgr, 359, 959).B);
        Assert.Equal((byte)2, Read(bgr, 359, 959).G);
        Assert.Equal((byte)3, Read(bgr, 359, 959).R);
    }

    [Fact]
    public void Encode_RejectsWrongSurfaceSize()
    {
        var source = CreateLandscapeBgra();
        Assert.Throws<ArgumentException>(() => BgrFrameEncoder.Encode(source, 360, 960, 90));
    }

    [Fact]
    public void SourceSize_MatchesRotation()
    {
        Assert.Equal((960, 360), BgrFrameEncoder.SourceSize(90));
        Assert.Equal((960, 360), BgrFrameEncoder.SourceSize(270));
        Assert.Equal((360, 960), BgrFrameEncoder.SourceSize(0));
        Assert.Equal((360, 960), BgrFrameEncoder.SourceSize(180));
    }
}
