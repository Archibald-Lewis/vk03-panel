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
    public static void Playback(DrawingContext dc, PanelViewModel m, IPanelTheme theme,
        Brush ink, Brush accent, Brush track, Brush playInk)
    {
        if (m.PressedAction is { } pressed && pressed is not (PanelAction.Play or PanelAction.Lock or PanelAction.CycleTheme)
            && !theme.Bounds(pressed).IsEmpty)
            Paint.RoundedRect(dc, Rect.Inflate(theme.Bounds(pressed), -2, -2), 10,
                Paint.Brush(m.DarkMode ? Color.FromArgb(24, 255, 255, 255) : Color.FromArgb(18, 0, 0, 0)));
        var previous = Center(theme.Bounds(PanelAction.Previous));
        var next = Center(theme.Bounds(PanelAction.Next));
        MacGlyphs.Previous(dc, Box(previous.X, previous.Y, 22), ink);
        MacGlyphs.Next(dc, Box(next.X, next.Y, 22), ink);
        var play = Center(theme.Bounds(PanelAction.Play));
        var radius = theme.Bounds(PanelAction.Play).Width / 2 - 7;
        dc.DrawEllipse(m.PressedAction == PanelAction.Play ? ink : accent, null, play, radius, radius);
        if (m.Media.Playing) MacGlyphs.Pause(dc, Box(play.X, play.Y, 25), playInk);
        else MacGlyphs.Play(dc, Box(play.X + 2, play.Y, 25), playInk);
        var mute = Center(theme.Bounds(PanelAction.Mute));
        Win11Glyphs.Speaker(dc, Box(mute.X, mute.Y, 23), m.Muted ? accent : ink, m.Muted);
        var volume = Center(theme.Bounds(PanelAction.Volume));
        var fraction = m.Muted ? 0 : Math.Clamp(m.Volume ?? 0, 0, 1);
        Paint.Bar(dc, new Rect(theme.SliderLeft, volume.Y - 1.5, theme.SliderWidth, 3), fraction, track, accent);
        if (m.Volume.HasValue)
            dc.DrawEllipse(accent, null, new Point(theme.SliderLeft + fraction * theme.SliderWidth, volume.Y), 4, 4);
    }
    public static void Lock(DrawingContext dc, PanelViewModel m, IPanelTheme theme, Brush ink, Brush track, bool label)
    {
        var r = theme.Bounds(PanelAction.Lock);
        var c = Center(r);
        if (m.PressedAction == PanelAction.Lock)
            Paint.RoundedRect(dc, Rect.Inflate(r, -2, -2), 10, track);
        Win11Glyphs.Lock(dc, Box(label ? r.Left + 16 : c.X, c.Y, 19), m.CanLock ? ink : track);
        if (label) Paint.Text(dc, m.LockProgress > 0 ? "保持按住" : "按住锁屏", r.Left + 32, c.Y - 7, 11, ink);
        if (m.LockProgress > 0)
            Paint.Bar(dc, new Rect(r.Left + 6, r.Bottom - 4, r.Width - 12, 2), m.LockProgress, track, ink);
    }
}
