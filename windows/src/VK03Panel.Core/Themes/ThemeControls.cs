using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace VK03Panel.Core.Themes;

internal static class ThemeControls
{
    public static string Number(double? n, string format = "0") => n.HasValue && double.IsFinite(n.Value)
        ? n.Value.ToString(format, CultureInfo.InvariantCulture) : "—";
    public static string Percent(double? n) => n.HasValue && double.IsFinite(n.Value) ? Number(n * 100) + "%" : "—";
    public static Rect Box(double x, double y, double size) => new(x - size / 2, y - size / 2, size, size);
    public static Point Center(Rect r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);
    public static PanelAction? Hit(IPanelTheme theme, Point point)
    {
        foreach (var a in Enum.GetValues<PanelAction>())
            if (!theme.Bounds(a).IsEmpty && theme.Bounds(a).Contains(point)) return a;
        return null;
    }
    public static void Cover(DrawingContext dc, PanelMediaState media, Rect bounds, double radius, Brush fill, Brush ink)
    {
        Paint.ClipRounded(dc, bounds, radius, () =>
        {
            if (media.Artwork is { } art)
                dc.DrawRectangle(new ImageBrush(art) { Stretch = Stretch.UniformToFill }, null, bounds);
            else
            {
                dc.DrawRectangle(fill, null, bounds);
                var c = Center(bounds);
                MacGlyphs.MusicNote(dc, Box(c.X, c.Y, Math.Min(42, bounds.Width * .45)), ink);
            }
        });
    }
}
