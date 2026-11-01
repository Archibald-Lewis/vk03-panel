namespace VK03Panel.Core.Rendering;

/// <summary>
/// Synthetic panel pattern used by the transport self-test. It is deliberately asymmetric
/// (coloured corner blocks plus a hard top band) so orientation and channel order mistakes
/// are obvious on the physical panel.
/// </summary>
public static class TestPattern
{
    public const int LandscapeWidth = 960;
    public const int LandscapeHeight = 360;

    public static byte[] CreateLandscapeBgra()
    {
        var bgra = new byte[LandscapeWidth * LandscapeHeight * 4];

        // Background: dark vertical gradient.
        for (var y = 0; y < LandscapeHeight; y++)
        {
            var t = y / (double)(LandscapeHeight - 1);
            var r = (byte)(16 + (10 * t));
            var g = (byte)(18 + (12 * t));
            var b = (byte)(24 + (16 * t));
            for (var x = 0; x < LandscapeWidth; x++)
            {
                var index = ((y * LandscapeWidth) + x) * 4;
                bgra[index] = b;
                bgra[index + 1] = g;
                bgra[index + 2] = r;
                bgra[index + 3] = 0xFF;
            }
        }

        // Eight vertical colour bars across the middle band.
        byte[][] bars =
        {
            new byte[] { 255, 255, 255 },
            new byte[] { 0, 255, 255 },
            new byte[] { 255, 255, 0 },
            new byte[] { 0, 255, 0 },
            new byte[] { 255, 0, 255 },
            new byte[] { 0, 0, 255 },
            new byte[] { 255, 0, 0 },
            new byte[] { 0, 0, 0 },
        };

        var barWidth = LandscapeWidth / bars.Length;
        for (var bar = 0; bar < bars.Length; bar++)
        {
            var startX = bar * barWidth;
            var endX = bar == bars.Length - 1 ? LandscapeWidth : startX + barWidth;
            var bgr = bars[bar];
            for (var y = 30; y < 330; y++)
            {
                for (var x = startX; x < endX; x++)
                {
                    var index = ((y * LandscapeWidth) + x) * 4;
                    bgra[index] = bgr[0];
                    bgra[index + 1] = bgr[1];
                    bgra[index + 2] = bgr[2];
                }
            }
        }

        // Top band marks the intended "up" direction for the landscape layout.
        FillRect(bgra, 0, 0, LandscapeWidth, 26, 0xF2, 0xF2, 0xF2);

        // Corner markers: white top-left, red top-right, green bottom-left, blue bottom-right.
        FillRect(bgra, 0, 0, 56, 56, 255, 255, 255);
        FillRect(bgra, LandscapeWidth - 56, 0, 56, 56, 0, 0, 255);
        FillRect(bgra, 0, LandscapeHeight - 56, 56, 56, 0, 255, 0);
        FillRect(bgra, LandscapeWidth - 56, LandscapeHeight - 56, 56, 56, 255, 0, 0);

        // Centre crosshair.
        FillRect(bgra, (LandscapeWidth / 2) - 2, 60, 4, 240, 255, 255, 255);
        FillRect(bgra, 240, (LandscapeHeight / 2) - 2, LandscapeWidth - 480, 4, 255, 255, 255);

        return bgra;
    }

    private static void FillRect(byte[] bgra, int left, int top, int width, int height, byte r, byte g, byte b)
    {
        var right = Math.Min(LandscapeWidth, left + width);
        var bottom = Math.Min(LandscapeHeight, top + height);
        for (var y = Math.Max(0, top); y < bottom; y++)
        {
            for (var x = Math.Max(0, left); x < right; x++)
            {
                var index = ((y * LandscapeWidth) + x) * 4;
                bgra[index] = b;
                bgra[index + 1] = g;
                bgra[index + 2] = r;
                bgra[index + 3] = 0xFF;
            }
        }
    }
}
