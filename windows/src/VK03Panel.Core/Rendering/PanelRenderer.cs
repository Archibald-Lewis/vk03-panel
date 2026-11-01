using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VK03Panel.Core.Themes;

namespace VK03Panel.Core.Rendering;

/// <summary>A rendered panel frame: raw BGRA surface plus the bitmap used for previews.</summary>
public sealed record RenderedFrame(byte[] Bgra, int Width, int Height, BitmapSource Bitmap);

/// <summary>
/// Renders a theme into a bitmap. Themes always paint in the landscape 960×360 design space;
/// portrait orientations rotate that design space into the 360×960 surface before the pixel
/// rotation performed by <see cref="BgrFrameEncoder"/>.
/// </summary>
public static class PanelRenderer
{
    public const int DesignWidth = 960;
    public const int DesignHeight = 360;

    public static RenderedFrame Render(IPanelTheme theme, PanelViewModel model, int rotation)
    {
        if (!BgrFrameEncoder.IsSupportedRotation(rotation))
        {
            throw new ArgumentOutOfRangeException(nameof(rotation), rotation, "仅支持 0/90/180/270。");
        }

        var (width, height) = BgrFrameEncoder.SourceSize(rotation);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var portrait = rotation is 0 or 180;
            if (portrait)
            {
                context.PushTransform(new TranslateTransform(360, 0));
                context.PushTransform(new RotateTransform(90));
            }

            theme.Draw(model, context, new Size(DesignWidth, DesignHeight));

            if (portrait)
            {
                context.Pop();
                context.Pop();
            }
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        return new RenderedFrame(pixels, width, height, bitmap);
    }

    /// <summary>Renders a frame and encodes it into the panel's native BGR888 buffer.</summary>
    public static (RenderedFrame Frame, byte[] Bgr) RenderEncoded(IPanelTheme theme, PanelViewModel model, int rotation)
    {
        var frame = Render(theme, model, rotation);
        var bgr = BgrFrameEncoder.Encode(frame.Bgra, frame.Width, frame.Height, rotation);
        return (frame, bgr);
    }

    public static void SavePng(BitmapSource bitmap, string path)
    {
        var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = System.IO.File.Create(path);
        encoder.Save(stream);
    }
}
