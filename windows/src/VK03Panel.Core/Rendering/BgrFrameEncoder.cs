namespace VK03Panel.Core.Rendering;

/// <summary>
/// Converts a 32-bit BGRA surface into the panel's native 360×960 BGR888 buffer,
/// applying the same rotation mapping as the macOS renderer. The caller renders a
/// landscape 960×360 surface for 90°/270° and a portrait 360×960 surface for 0°/180°.
/// </summary>
public static class BgrFrameEncoder
{
    public const int PanelWidth = 360;
    public const int PanelHeight = 960;
    public const int PixelStride = 4;
    public const int OutputPixelStride = 3;

    public static byte[] Encode(ReadOnlySpan<byte> bgra, int sourceWidth, int sourceHeight, int rotation)
    {
        var landscape = rotation is 90 or 270;
        var expectedWidth = landscape ? 960 : 360;
        var expectedHeight = landscape ? 360 : 960;

        if (sourceWidth != expectedWidth || sourceHeight != expectedHeight)
        {
            throw new ArgumentException(
                $"方向 {rotation}° 需要 {expectedWidth}×{expectedHeight} 的源画面，实际 {sourceWidth}×{sourceHeight}。",
                nameof(bgra));
        }

        if (bgra.Length < sourceWidth * sourceHeight * PixelStride)
        {
            throw new ArgumentException("源像素缓冲区长度不足。", nameof(bgra));
        }

        var output = new byte[PanelWidth * PanelHeight * OutputPixelStride];

        for (var y = 0; y < PanelHeight; y++)
        {
            for (var x = 0; x < PanelWidth; x++)
            {
                int sourceX;
                int sourceY;
                switch (rotation)
                {
                    case 90:
                        sourceX = y;
                        sourceY = 359 - x;
                        break;
                    case 180:
                        sourceX = 359 - x;
                        sourceY = 959 - y;
                        break;
                    case 270:
                        sourceX = 959 - y;
                        sourceY = x;
                        break;
                    default:
                        sourceX = x;
                        sourceY = y;
                        break;
                }

                var source = ((sourceY * sourceWidth) + sourceX) * PixelStride;
                var destination = ((y * PanelWidth) + x) * OutputPixelStride;
                output[destination] = bgra[source];
                output[destination + 1] = bgra[source + 1];
                output[destination + 2] = bgra[source + 2];
            }
        }

        return output;
    }

    /// <summary>Checks whether the given rotation value is supported.</summary>
    public static bool IsSupportedRotation(int rotation) => rotation is 0 or 90 or 180 or 270;

    /// <summary>Returns the source surface size the renderer must produce for a rotation.</summary>
    public static (int Width, int Height) SourceSize(int rotation) =>
        rotation is 90 or 270 ? (960, 360) : (360, 960);
}
