using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VK03Panel.Core.Media;

/// <summary>Severs BitmapFrame decoder affinity before sharing artwork across render threads.</summary>
internal static class ArtworkFrames
{
    public static BitmapSource Detach(BitmapSource source)
    {
        BitmapSource pixels = source;
        var largest = Math.Max(source.PixelWidth, source.PixelHeight);
        if (largest > 1024)
        {
            var scale = 1024.0 / largest;
            pixels = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        }
        var converted = new FormatConvertedBitmap(pixels, PixelFormats.Pbgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var data = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(data, stride, 0);
        var copy = BitmapSource.Create(converted.PixelWidth, converted.PixelHeight, 96, 96,
            PixelFormats.Pbgra32, null, data, stride);
        copy.Freeze();
        return copy;
    }
}
