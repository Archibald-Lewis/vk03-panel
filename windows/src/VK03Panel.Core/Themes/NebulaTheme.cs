using System.Globalization;
using System.Windows;
using System.Windows.Media;
using static VK03Panel.Core.Themes.ThemeControls;

namespace VK03Panel.Core.Themes;

/// <summary>Deep-space instruments with one calibrated accent and a single media rail.</summary>
public sealed class NebulaTheme : IPanelTheme
{
    private static readonly FontFamily Face = new("Bahnschrift, Segoe UI, Microsoft YaHei UI");
    public string Id => "nebula";
    public string DisplayName => "星环";
    public string Description => "深空背景、精密环形仪表与统一媒体栏";
    public Size DesignSize => new(960, 360);
    public double SliderLeft => 668;
    public double SliderWidth => 148;
    public Rect Bounds(PanelAction a) => a switch
    {
        PanelAction.CycleTheme => new Rect(144, 182, 64, 48),
        PanelAction.Previous => new Rect(360, 272, 56, 56),
        PanelAction.Play => new Rect(424, 264, 72, 72),
        PanelAction.Next => new Rect(504, 272, 56, 56),
        PanelAction.Mute => new Rect(600, 276, 48, 48),
        PanelAction.Volume => new Rect(660, 276, 172, 48),
        PanelAction.Lock => new Rect(856, 276, 64, 48),
        _ => Rect.Empty,
    };
    public PanelAction? HitTest(Point point) => Hit(this, point);
    public void Draw(PanelViewModel m, DrawingContext dc, Size size)
    {
        var dark = m.DarkMode;
        var accentColor = dark ? Color.FromRgb(131, 223, 224) : Color.FromRgb(16, 103, 116);
        var accent = Paint.Brush(accentColor);
        var ink = Paint.Brush(dark ? Color.FromRgb(234, 244, 246) : Color.FromRgb(23, 43, 53));
        var muted = Paint.Brush(dark ? Color.FromRgb(145, 168, 181) : Color.FromRgb(83, 105, 116));
        var track = Paint.Brush(Paint.WithAlpha(accentColor, .18));
        var surface = new Rect(new Point(), size);
        dc.DrawImage(ThemeArtwork.Get("nebula-space.png"), surface);
        if (!dark)
            Paint.LinearGradient(dc, surface, new Point(0, 0), new Point(1, 0),
                (0, Color.FromArgb(250, 231, 241, 242)), (.70, Color.FromArgb(244, 231, 241, 242)),
                (1, Color.FromArgb(202, 231, 241, 242)));
        else
            Paint.LinearGradient(dc, surface, new Point(0, 0), new Point(1, 0),
                (0, Color.FromArgb(24, 7, 15, 21)), (.70, Color.FromArgb(72, 7, 15, 21)),
                (1, Color.FromArgb(95, 7, 15, 21)));
        dc.DrawLine(Paint.Stroke(track, .8), new Point(236, 32), new Point(236, 228));
        dc.DrawLine(Paint.Stroke(track, .8), new Point(686, 44), new Point(686, 228));
        T(dc, m.Time.ToString("HH:mm", CultureInfo.InvariantCulture), 34, 47, 46, ink, TextWeight.Medium, true);
        T(dc, m.Time.ToString("M月d日 ddd", CultureInfo.GetCultureInfo("zh-CN")), 38, 112, 14, muted);
        dc.DrawEllipse(m.PanelConnected ? accent : muted, null, new Point(41, 158), 3, 3);
        T(dc, m.Demo ? "演示数据" : m.PanelConnected ? "副屏在线" : "等待连接", 52, 151, 11, muted);
        if (m.PressedAction == PanelAction.CycleTheme)
            Paint.RoundedRect(dc, Bounds(PanelAction.CycleTheme), 8, track);
        T(dc, "主题 ›", 155, 198, 12, muted);

        var cpu = new Point(348, 125);
        Ring(dc, cpu, 76, m.CpuLoad, accentColor);
        Paint.CenteredText(dc, "CPU", 348, 84, 12, muted, family: Face);
        Paint.CenteredText(dc, Percent(m.CpuLoad), 348, 109, 39, ink, TextWeight.Medium, family: Face);
        Paint.CenteredText(dc, Number(m.CpuTemperatureCelsius) + " °C", 348, 157, 13, muted, family: Face);
        Paint.CenteredText(dc, Number(m.CpuFrequencyGhz, "0.00") + " GHz", 348, 207, 12, muted, family: Face);

        T(dc, "内存", 478, 47, 14, muted);
        T(dc, Number(m.MemoryUsedGb, "0.0"), 476, 81, 38, ink, TextWeight.Medium, true, 142);
        T(dc, "GB", 631, 104, 13, muted);
        T(dc, "共 " + Number(m.MemoryTotalGb) + " GB", 480, 137, 13, muted, maxWidth: 174);
        Paint.Bar(dc, new Rect(480, 176, 174, 4), m.MemoryFraction, track, accent);
        Paint.Sparkline(dc, new Rect(480, 202, 174, 24), m.MemoryHistory, accentColor);

        T(dc, "GPU", 714, 47, 14, muted);
        T(dc, Percent(m.GpuLoad), 712, 81, 38, ink, TextWeight.Medium, true, 190);
        T(dc, Number(m.GpuTemperatureCelsius) + " °C", 715, 137, 13, muted);
        Paint.Sparkline(dc, new Rect(714, 175, 196, 35), m.GpuLoadHistory, accentColor);
        T(dc, "近期峰值 " + Peak(m.GpuLoadHistory), 714, 223, 11, muted);

        var rail = new Rect(20, 252, 920, 88);
        Paint.RoundedRect(dc, rail, 18, Paint.Brush(dark ? Color.FromArgb(218, 15, 28, 37) : Color.FromArgb(223, 246, 250, 250)),
            Paint.Stroke(Paint.Brush(Paint.WithAlpha(accentColor, .16)), 1));
        ThemeControls.Cover(dc, m.Media, new Rect(36, 272, 48, 48), 7, track, accent);
        Paint.Text(dc, string.IsNullOrWhiteSpace(m.Media.Title) ? "暂无播放" : m.Media.Title, 100, 274, 17, ink, TextWeight.Medium, maxWidth: 236);
        Paint.Text(dc, string.IsNullOrWhiteSpace(m.Media.Artist) ? (m.Media.HasSession ? "" : m.PlaybackMessage ?? "等待媒体播放") : m.Media.Artist,
            100, 302, 12, muted, maxWidth: 236);
        Playback(dc, m, this, ink, accent, track, Paint.Brush(dark ? Color.FromRgb(12, 29, 36) : Color.FromRgb(243, 249, 250)));
        Lock(dc, m, this, muted, track, false);
        if (m.PressedAction is PanelAction.Volume or PanelAction.Mute)
            T(dc, m.Muted ? "已静音" : Percent(m.Volume), 764, 260, 10, muted);
    }
    private static void Ring(DrawingContext dc, Point center, double radius, double? fraction, Color color)
    {
        var value = double.IsFinite(fraction ?? 0) ? Math.Clamp(fraction ?? 0, 0, 1) : 0;
        Arc(dc, center, radius, 135, 405, Paint.Stroke(Paint.Brush(Paint.WithAlpha(color, .16)), 4));
        if (value > 0)
        {
            Arc(dc, center, radius, 135, 135 + 270 * value, Paint.Stroke(Paint.Brush(Paint.WithAlpha(color, .12)), 12));
            Arc(dc, center, radius, 135, 135 + 270 * value, Paint.Stroke(Paint.Brush(color), 3));
        }
        for (var i = 0; i <= 36; i++)
        {
            var angle = 135 + 270.0 * i / 36;
            dc.DrawLine(Paint.Stroke(Paint.Brush(Paint.WithAlpha(color, i / 36.0 <= value && fraction.HasValue ? .85 : .22)), 1),
                At(center, radius + 8, angle), At(center, radius + (i % 6 == 0 ? 15 : 11), angle));
        }
    }
    private static void Arc(DrawingContext dc, Point c, double r, double start, double end, Pen pen)
    {
        var f = new PathFigure { StartPoint = At(c, r, start), IsClosed = false, IsFilled = false };
        f.Segments.Add(new ArcSegment(At(c, r, end), new Size(r, r), 0, end - start > 180, SweepDirection.Clockwise, true));
        var g = new PathGeometry(new[] { f }); g.Freeze(); dc.DrawGeometry(null, pen, g);
    }
    private static Point At(Point c, double r, double a) => new(c.X + Math.Cos(a * Math.PI / 180) * r, c.Y + Math.Sin(a * Math.PI / 180) * r);
    private static string Peak(IReadOnlyList<double> h)
    {
        var valid = h.Where(double.IsFinite).ToArray();
        return valid.Length > 0 ? Percent(valid.Max()) : "—";
    }
    private static void T(DrawingContext dc, string text, double x, double y, double size, Brush brush,
        TextWeight weight = TextWeight.Regular, bool tabular = false, double? maxWidth = null)
        => Paint.Text(dc, text, x, y, size, brush, weight, tabular, maxWidth, Face);
}
