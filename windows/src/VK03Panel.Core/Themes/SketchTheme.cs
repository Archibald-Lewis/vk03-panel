using System.Globalization;
using System.Windows;
using System.Windows.Media;
using static VK03Panel.Core.Themes.ThemeControls;

namespace VK03Panel.Core.Themes;

/// <summary>Pastel cartoon stationery with stable hand-drawn outlines and a headphone cat.</summary>
public sealed class SketchTheme : IPanelTheme
{
    public string Id => "sketch";
    public string DisplayName => "手绘卡通";
    public string Description => "蜡笔猫咪、奶油纸张与手画仪表";
    public Size DesignSize => new(960, 360);
    public double SliderLeft => 674;
    public double SliderWidth => 224;
    public Rect Bounds(PanelAction a) => a switch
    {
        PanelAction.Lock => new Rect(576, 16, 96, 48),
        PanelAction.Previous => new Rect(360, 272, 56, 56),
        PanelAction.Play => new Rect(424, 264, 72, 72),
        PanelAction.Next => new Rect(504, 272, 56, 56),
        PanelAction.Mute => new Rect(600, 276, 48, 48),
        PanelAction.Volume => new Rect(660, 276, 260, 48),
        _ => Rect.Empty,
    };
    public PanelAction? HitTest(Point p) => Hit(this, p);
    public void Draw(PanelViewModel m, DrawingContext dc, Size size)
    {
        var dark = m.DarkMode;
        dc.DrawImage(ThemeArtwork.Get(dark ? "sketch-cat-dark.png" : "sketch-cat-light.png"), new Rect(new Point(), size));
        var ink = Paint.Brush(dark ? Color.FromRgb(243, 240, 223) : Color.FromRgb(55, 61, 52));
        var muted = Paint.Brush(dark ? Color.FromRgb(185, 195, 179) : Color.FromRgb(107, 115, 96));
        var line = Paint.Brush(dark ? Color.FromRgb(126, 146, 129) : Color.FromRgb(77, 88, 70));
        var accent = Paint.Brush(dark ? Color.FromRgb(229, 184, 151) : Color.FromRgb(239, 183, 143));
        var track = Paint.Brush(dark ? Color.FromRgb(80, 94, 81) : Color.FromRgb(198, 206, 177));
        Paint.Text(dc, m.Time.ToString("HH:mm", CultureInfo.InvariantCulture), 30, 24, 42, ink, TextWeight.Medium, tabular: true);
        Paint.Text(dc, m.Time.ToString("M月d日 ddd", CultureInfo.GetCultureInfo("zh-CN")), 190, 35, 14, muted);
        if (m.Demo) Paint.Text(dc, "演示数据", 360, 37, 11, muted);
        Metric(dc, new Rect(28, 98, 188, 132), "CPU", Percent(m.CpuLoad), Number(m.CpuTemperatureCelsius) + " °C · " + Number(m.CpuFrequencyGhz, "0.00") + " GHz",
            m.CpuLoadHistory, dark ? Color.FromRgb(48, 66, 54) : Color.FromRgb(227, 239, 213), ink, muted, line, Color.FromRgb(110, 157, 119));
        Metric(dc, new Rect(232, 98, 188, 132), "内存", Number(m.MemoryUsedGb, "0.0") + " GB", "共 " + Number(m.MemoryTotalGb) + " GB",
            m.MemoryHistory, dark ? Color.FromRgb(66, 63, 47) : Color.FromRgb(250, 234, 192), ink, muted, line, dark ? Color.FromRgb(217, 196, 128) : Color.FromRgb(163, 134, 65));
        Metric(dc, new Rect(436, 98, 188, 132), "GPU", Percent(m.GpuLoad), Number(m.GpuTemperatureCelsius) + " °C",
            m.GpuLoadHistory, dark ? Color.FromRgb(70, 54, 48) : Color.FromRgb(248, 222, 202), ink, muted, line, dark ? Color.FromRgb(217, 161, 134) : Color.FromRgb(176, 119, 92));
        RoughRect(dc, new Rect(24, 256, 912, 80), Paint.Brush(dark ? Color.FromRgb(43, 52, 45) : Color.FromRgb(253, 246, 222)), line);
        ThemeControls.Cover(dc, m.Media, new Rect(40, 272, 48, 48), 8, track, ink);
        Paint.Text(dc, string.IsNullOrWhiteSpace(m.Media.Title) ? "暂无播放" : m.Media.Title, 104, 274, 17, ink, TextWeight.Medium, maxWidth: 232);
        Paint.Text(dc, string.IsNullOrWhiteSpace(m.Media.Artist) ? (m.Media.HasSession ? "" : m.PlaybackMessage ?? "等待媒体播放") : m.Media.Artist,
            104, 302, 12, muted, maxWidth: 232);
        Playback(dc, m, this, ink, accent, track, Paint.Brush(Color.FromRgb(53, 59, 48)));
        dc.DrawEllipse(null, Paint.Stroke(line, 1.4), new Point(459, 300), 30, 29);
        Lock(dc, m, this, muted, track, true);
        if (m.PressedAction is PanelAction.Volume or PanelAction.Mute)
            Paint.Text(dc, m.Muted ? "已静音" : Percent(m.Volume), 858, 267, 10, muted);
    }
    private static void Metric(DrawingContext dc, Rect r, string name, string value, string detail, IReadOnlyList<double> history,
        Color fill, Brush ink, Brush muted, Brush line, Color chart)
    {
        RoughRect(dc, r, Paint.Brush(fill), line);
        Paint.Text(dc, name, r.Left + 16, r.Top + 12, 13, muted, TextWeight.Medium);
        Paint.Text(dc, value, r.Left + 15, r.Top + 39, 30, ink, TextWeight.Medium, maxWidth: r.Width - 28);
        Paint.Text(dc, detail, r.Left + 16, r.Top + 82, 11, muted, maxWidth: r.Width - 28);
        Paint.Sparkline(dc, new Rect(r.Left + 17, r.Top + 108, r.Width - 34, 13), history, chart);
    }
    private static void RoughRect(DrawingContext dc, Rect r, Brush fill, Brush line)
    {
        // Deterministic curves keep the drawn-paper edges still during touch redraws.
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(r.Left + 12, r.Top + 1), true, true);
            c.BezierTo(new Point(r.Left + r.Width * .36, r.Top - 1), new Point(r.Right - 34, r.Top + 2), new Point(r.Right - 10, r.Top), true, false);
            c.QuadraticBezierTo(new Point(r.Right + 1, r.Top), new Point(r.Right, r.Top + 12), true, false);
            c.LineTo(new Point(r.Right + 1, r.Bottom - 11), true, false);
            c.QuadraticBezierTo(new Point(r.Right, r.Bottom + 1), new Point(r.Right - 12, r.Bottom), true, false);
            c.BezierTo(new Point(r.Left + r.Width * .64, r.Bottom + 2), new Point(r.Left + 32, r.Bottom - 1), new Point(r.Left + 11, r.Bottom + 1), true, false);
            c.QuadraticBezierTo(new Point(r.Left - 1, r.Bottom), new Point(r.Left, r.Bottom - 12), true, false);
            c.LineTo(new Point(r.Left - 1, r.Top + 12), true, false);
            c.QuadraticBezierTo(new Point(r.Left, r.Top), new Point(r.Left + 12, r.Top + 1), true, false);
        }
        g.Freeze();
        dc.DrawGeometry(fill, Paint.Stroke(line, 1.6), g);
        dc.DrawLine(Paint.Stroke(line, .55), new Point(r.Left + 10, r.Bottom + 3), new Point(r.Left + r.Width * .42, r.Bottom + 2));
    }
}
