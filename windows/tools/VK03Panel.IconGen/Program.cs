using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace VK03Panel.IconGen;

/// <summary>
/// Generates the multi-resolution application icon (assets/app.ico) with the same design as the
/// macOS project: an azure rounded tile holding a monitor outline and dashboard bars.
/// </summary>
internal static class Program
{
    private static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };

    private static int Main(string[] args)
    {
        var root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        var assets = Path.GetFullPath(root);
        Directory.CreateDirectory(assets);

        var entries = new List<(int Size, byte[] Data)>();
        foreach (var size in Sizes)
        {
            using var bitmap = DrawIcon(size);
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            entries.Add((size, stream.ToArray()));
        }

        var icoPath = Path.Combine(assets, "app.ico");
        WriteIcon(icoPath, entries);

        using (var preview = DrawIcon(256))
        {
            preview.Save(Path.Combine(assets, "app-icon.png"), ImageFormat.Png);
        }

        Console.WriteLine($"已生成 {icoPath}");
        return 0;
    }

    private static Bitmap DrawIcon(int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.Clear(Color.Transparent);

        var scale = size / 256f;
        graphics.ScaleTransform(scale, scale);

        var tile = new RectangleF(12, 12, 232, 232);
        using (var path = RoundedRect(tile, 52))
        using (var gradient = new LinearGradientBrush(
                   new PointF(20, 240),
                   new PointF(230, 10),
                   Color.FromArgb(0x36, 0xAD, 0xFF),
                   Color.FromArgb(0x12, 0x57, 0xEA)))
        {
            graphics.FillPath(gradient, path);
        }

        // Dashboard screen outline.
        using (var pen = new Pen(Color.FromArgb(235, 255, 255, 255), 9f) { LineJoin = LineJoin.Round })
        using (var screen = RoundedRect(new RectangleF(58, 58, 140, 140), 24))
        {
            graphics.DrawPath(pen, screen);
        }

        // Three data bars, brighter at the top like the macOS icon.
        var bars = new (float Y, float Width, int Alpha)[]
        {
            (150, 62, 235),
            (120, 86, 200),
            (90, 74, 165),
        };
        foreach (var (y, width, alpha) in bars)
        {
            using var brush = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255));
            using var bar = RoundedRect(new RectangleF(78, y, width, 10), 5);
            graphics.FillPath(brush, bar);
        }

        return bitmap;
    }

    private static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void WriteIcon(string path, List<(int Size, byte[] Data)> entries)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0);              // reserved
        writer.Write((ushort)1);              // icon type
        writer.Write((ushort)entries.Count);  // image count

        var offset = 6 + (16 * entries.Count);
        foreach (var (size, data) in entries)
        {
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);   // palette colour count
            writer.Write((byte)0);   // reserved
            writer.Write((ushort)1); // colour planes
            writer.Write((ushort)32);
            writer.Write(data.Length);
            writer.Write(offset);
            offset += data.Length;
        }

        foreach (var (_, data) in entries)
        {
            writer.Write(data);
        }
    }
}
