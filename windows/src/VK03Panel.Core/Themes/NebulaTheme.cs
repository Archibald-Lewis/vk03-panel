using System.Globalization;
using System.Windows;
using System.Windows.Media;
using static VK03Panel.Core.Themes.ThemeControls;
using static VK03Panel.Core.Themes.ThemeDrawing;

namespace VK03Panel.Core.Themes;

/// <summary>Two precision instruments, memory rack and physical transport keys.</summary>
public sealed class NebulaTheme : IPanelTheme
{
    private static readonly FontFamily Face = new("Bahnschrift, Segoe UI, Microsoft YaHei UI");
    public string Id => "nebula";
    public string DisplayName => "星环";
    public string Description => "双精密仪表、内存刻度与实体媒体按键";
    public Size DesignSize => new(960, 360);
    public double SliderLeft => 728;
    public double SliderWidth => 176;
    public Rect Bounds(PanelAction a) => a switch
    {
        PanelAction.Lock => new Rect(856, 28, 64, 48),
        PanelAction.Previous => new Rect(322, 272, 56, 56),
        PanelAction.Play => new Rect(386, 264, 72, 72),
        PanelAction.Next => new Rect(466, 272, 56, 56),
        PanelAction.Mute => new Rect(648, 272, 56, 56),
        PanelAction.Volume => new Rect(720, 272, 200, 56),
        _ => Rect.Empty,
    };
    public PanelAction? HitTest(Point p) => Hit(this, p);
    public void Draw(PanelViewModel m, DrawingContext dc, Size size)
    {
        var dark = m.DarkMode;
        var color = dark ? Color.FromRgb(131, 223, 224) : Color.FromRgb(17, 108, 123);
        var accent = Paint.Brush(color);
        var ink = Paint.Brush(dark ? Color.FromRgb(232, 244, 246) : Color.FromRgb(23, 45, 56));
        var muted = Paint.Brush(dark ? Color.FromRgb(145, 174, 186) : Color.FromRgb(75, 105, 118));
        var edge = Paint.Brush(Paint.WithAlpha(color, dark ? .28 : .35));
        var track = Paint.Brush(Paint.WithAlpha(color, .22));
        var fill = Paint.Brush(dark ? Color.FromRgb(24, 43, 54) : Color.FromRgb(231, 242, 244));
        dc.DrawImage(ThemeArtwork.Get("nebula-space.png"), new Rect(new Point(), size));
        dc.DrawRectangle(Paint.Brush(dark ? Color.FromArgb(84, 7, 16, 23) : Color.FromArgb(230, 224, 236, 240)), null, new Rect(new Point(), size));
        Panel(dc, new Rect(20, 20, 264, 220), dark, edge);
        Panel(dc, new Rect(296, 20, 240, 220), dark, edge);
        Panel(dc, new Rect(548, 20, 392, 220), dark, edge);
        T(dc, "CPU", 40, 32, 12, muted);
        T(dc, "GPU", 316, 32, 12, muted);
        Instrument(dc, new Point(152, 126), 69, m.CpuLoad, Number(m.CpuTemperatureCelsius) + " °C", dark, color, ink, muted);
        Instrument(dc, new Point(416, 126), 65, m.GpuLoad, Number(m.GpuTemperatureCelsius) + " °C", dark, color, ink, muted);
        Paint.CenteredText(dc, Number(m.CpuFrequencyGhz, "0.00") + " GHz", 152, 211, 12, muted, family: Face);
        Paint.CenteredText(dc, "近期峰值 " + Peak(m.GpuLoadHistory), 416, 211, 11, muted, family: Face);

        T(dc, m.Time.ToString("HH:mm", CultureInfo.InvariantCulture), 568, 36, 36, ink, TextWeight.Medium, true);
        T(dc, m.Time.ToString("M月d日 ddd", CultureInfo.GetCultureInfo("zh-CN")), 698, 54, 12, muted, maxWidth: 140);
        dc.DrawLine(Paint.Stroke(edge, 1), new Point(568, 92), new Point(920, 92));
        T(dc, "内存", 570, 108, 13, muted);
        T(dc, Number(m.MemoryUsedGb, "0.0"), 568, 134, 36, ink, TextWeight.Medium, maxWidth: 134);
        T(dc, "GB", 704, 155, 12, muted);
        T(dc, "共 " + Number(m.MemoryTotalGb) + " GB", 572, 181, 12, muted, maxWidth: 170);
        var known = m.MemoryUsedGb.HasValue && m.MemoryTotalGb is > 0;
        T(dc, known ? Percent(m.MemoryFraction) : "—", 815, 139, 31, accent, TextWeight.Medium, maxWidth: 104);
        for (var i = 0; i < 28; i++)
            dc.DrawRectangle(known && i < m.MemoryFraction * 28 ? accent : track, null, new Rect(572 + i * 12.2, 211, 8, i % 4 == 0 ? 10 : 7));

        Panel(dc, new Rect(20, 252, 602, 88), dark, edge);
        Panel(dc, new Rect(636, 252, 304, 88), dark, edge);
        ThemeControls.Cover(dc, m.Media, new Rect(36, 268, 56, 56), 3, track, accent);
        dc.DrawGeometry(null, Paint.Stroke(edge, 1), Cut(new Rect(33, 265, 62, 62), 5));
        Paint.Text(dc, string.IsNullOrWhiteSpace(m.Media.Title) ? "暂无播放" : m.Media.Title, 108, 273, 17, ink, TextWeight.Medium, maxWidth: 198);
        Paint.Text(dc, string.IsNullOrWhiteSpace(m.Media.Artist) ? (m.Media.HasSession ? "" : m.PlaybackMessage ?? "等待媒体播放") : m.Media.Artist,
            108, 302, 12, muted, maxWidth: 198);
        T(dc, m.Muted ? "已静音" : "音量", 728, 263, 10, muted);
        Controls(dc, m, this, ControlDrawingStyle.Instrument, ink, accent, fill, edge,
            Paint.Brush(dark ? Color.FromRgb(14, 34, 43) : Color.FromRgb(242, 250, 251)), false);
        if (m.Demo) T(dc, "演示数据", 32, 343, 9, muted);
    }
    private static void Instrument(DrawingContext dc, Point c, double r, double? fraction, string detail, bool dark, Color color, Brush ink, Brush muted)
    {
        var known = fraction.HasValue && double.IsFinite(fraction.Value);
        var value = known ? Math.Clamp(fraction!.Value, 0, 1) : 0;
        var bezel = new LinearGradientBrush(dark ? Color.FromRgb(71, 104, 117) : Color.FromRgb(129, 164, 174),
            dark ? Color.FromRgb(13, 31, 40) : Color.FromRgb(226, 242, 245), 55); bezel.Freeze();
        dc.DrawEllipse(Paint.Brush(dark ? Color.FromRgb(9, 23, 31) : Color.FromRgb(224, 238, 241)), Paint.Stroke(bezel, 6), c, r + 3, r + 3);
        dc.DrawEllipse(null, Paint.Stroke(Paint.Brush(Paint.WithAlpha(color, .12)), 1), c, r - 9, r - 9);
        Arc(dc, c, r - 3, 135, 405, Paint.Stroke(Paint.Brush(Paint.WithAlpha(color, .12)), 8));
        if (value > 0) Arc(dc, c, r - 3, 135, 135 + value * 270, Paint.Stroke(Paint.Brush(color), 4));
        for (var i = 0; i <= 40; i++)
        {
            var angle = 135 + i * 270.0 / 40;
            var major = i % 10 == 0;
            dc.DrawLine(Paint.Stroke(Paint.Brush(Paint.WithAlpha(color, known && i <= value * 40 ? .95 : .32)), major ? 1.4 : .7),
                At(c, r + 11, angle), At(c, r + (major ? 20 : 15), angle));
            if (major)
            {
                var at = At(c, r + 30, angle);
                Paint.CenteredText(dc, (i * 2.5).ToString("0", CultureInfo.InvariantCulture), at.X, at.Y - 5, 8, muted, family: Face);
            }
        }
        if (known)
        {
            var p = At(c, r - 3, 135 + value * 270);
            dc.DrawEllipse(Paint.Brush(color), null, p, 3.5, 3.5);
        }
        Paint.CenteredText(dc, Percent(fraction), c.X, c.Y - 22, r >= 69 ? 39 : 35, ink, TextWeight.Medium, family: Face);
        Paint.CenteredText(dc, detail, c.X, c.Y + 26, 12, muted, family: Face);
    }
    private static string Peak(IReadOnlyList<double> h)
    {
        var values = h.Where(double.IsFinite).ToArray(); return values.Length > 0 ? Percent(values.Max()) : "—";
    }
    private static void T(DrawingContext dc, string text, double x, double y, double size, Brush brush, TextWeight weight = TextWeight.Regular, bool tabular = false, double? maxWidth = null)
        => Paint.Text(dc, text, x, y, size, brush, weight, tabular, maxWidth, Face);
}
