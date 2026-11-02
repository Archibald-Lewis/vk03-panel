using System.Globalization;
using System.Windows;
using System.Windows.Media;
using static VK03Panel.Core.Themes.ThemeControls;

namespace VK03Panel.Core.Themes;

/// <summary>An original headphone character, music-led composition and live telemetry strip.</summary>
public sealed class AnimeTheme : IPanelTheme
{
    public string Id => "anime";
    public string DisplayName => "二次元";
    public string Description => "原创耳机角色、夜景与音乐控制台";
    public Size DesignSize => new(960, 360);
    public double SliderLeft => 502;
    public double SliderWidth => 176;
    public Rect Bounds(PanelAction a) => a switch
    {
        PanelAction.Lock => new Rect(612, 16, 104, 48),
        PanelAction.Previous => new Rect(190, 176, 56, 56),
        PanelAction.Play => new Rect(254, 168, 72, 72),
        PanelAction.Next => new Rect(334, 176, 56, 56),
        PanelAction.Mute => new Rect(430, 180, 48, 48),
        PanelAction.Volume => new Rect(494, 180, 204, 48),
        _ => Rect.Empty,
    };
    public PanelAction? HitTest(Point p) => Hit(this, p);
    public void Draw(PanelViewModel m, DrawingContext dc, Size size)
    {
        var dark = m.DarkMode;
        var surface = new Rect(new Point(), size);
        dc.DrawImage(ThemeArtwork.Get("anime-night.png"), surface);
        if (!dark)
            Paint.LinearGradient(dc, surface, new Point(0, 0), new Point(1, 0),
                (0, Color.FromArgb(250, 235, 241, 246)), (.62, Color.FromArgb(246, 235, 241, 246)),
                (.80, Color.FromArgb(22, 235, 241, 246)), (1, Color.FromArgb(0, 235, 241, 246)));
        else
            Paint.LinearGradient(dc, surface, new Point(0, 0), new Point(1, 0),
                (0, Color.FromArgb(58, 7, 18, 35)), (.66, Color.FromArgb(24, 7, 18, 35)), (1, Color.FromArgb(0, 7, 18, 35)));
        var ink = Paint.Brush(dark ? Color.FromRgb(239, 245, 255) : Color.FromRgb(32, 50, 71));
        var muted = Paint.Brush(dark ? Color.FromRgb(166, 187, 211) : Color.FromRgb(91, 112, 136));
        var rose = Paint.Brush(dark ? Color.FromRgb(241, 197, 208) : Color.FromRgb(139, 72, 96));
        var track = Paint.Brush(dark ? Color.FromArgb(60, 159, 190, 219) : Color.FromArgb(66, 106, 130, 159));
        Paint.Text(dc, m.Time.ToString("HH:mm", CultureInfo.InvariantCulture), 28, 24, 28, ink, TextWeight.Medium, tabular: true);
        Paint.Text(dc, m.Time.ToString("M月d日 ddd", CultureInfo.GetCultureInfo("zh-CN")), 132, 33, 12, muted);
        if (m.Demo) Paint.Text(dc, "演示数据", 302, 34, 11, muted);
        ThemeControls.Cover(dc, m.Media, new Rect(28, 86, 144, 144), 12, track, rose);
        Paint.Text(dc, string.IsNullOrWhiteSpace(m.Media.Title) ? "暂无播放" : m.Media.Title, 196, 91, 30, ink, TextWeight.Medium, maxWidth: 494);
        Paint.Text(dc, string.IsNullOrWhiteSpace(m.Media.Artist) ? (m.Media.HasSession ? "" : m.PlaybackMessage ?? "等待媒体播放") : m.Media.Artist,
            198, 137, 16, muted, maxWidth: 490);
        if (m.Media.DurationSeconds > 0)
            Paint.Bar(dc, new Rect(198, 163, 484, 2), m.Media.Progress, track, rose);
        Playback(dc, m, this, ink, rose, track, Paint.Brush(dark ? Color.FromRgb(31, 38, 58) : Color.FromRgb(251, 242, 246)));
        Lock(dc, m, this, muted, track, true);
        if (m.PressedAction is PanelAction.Volume or PanelAction.Mute)
            Paint.Text(dc, m.Muted ? "已静音" : Percent(m.Volume), 638, 229, 10, muted);
        Stats(dc, new Rect(28, 260, 204, 76), "CPU", Percent(m.CpuLoad), Number(m.CpuTemperatureCelsius) + " °C", m.CpuLoadHistory, dark, ink, muted);
        Stats(dc, new Rect(244, 260, 204, 76), "内存", Number(m.MemoryUsedGb, "0.0") + " GB", "共 " + Number(m.MemoryTotalGb) + " GB", m.MemoryHistory, dark, ink, muted);
        Stats(dc, new Rect(460, 260, 232, 76), "GPU", Percent(m.GpuLoad), Number(m.GpuTemperatureCelsius) + " °C", m.GpuLoadHistory, dark, ink, muted);
    }
    private static void Stats(DrawingContext dc, Rect r, string label, string value, string detail, IReadOnlyList<double> history, bool dark, Brush ink, Brush muted)
    {
        Paint.RoundedRect(dc, r, 12, Paint.Brush(dark ? Color.FromArgb(202, 17, 35, 57) : Color.FromArgb(219, 250, 252, 254)),
            Paint.Stroke(Paint.Brush(dark ? Color.FromArgb(35, 180, 208, 235) : Color.FromArgb(36, 106, 132, 163)), 1));
        Paint.Text(dc, label, r.Left + 16, r.Top + 10, 12, muted);
        Paint.Text(dc, detail, r.Left + 104, r.Top + 11, 11, muted, maxWidth: r.Width - 116);
        Paint.Text(dc, value, r.Left + 15, r.Top + 35, 27, ink, TextWeight.Medium, maxWidth: r.Width - 30);
        // History is intentionally subtle behind the right side; every point comes from real sampling.
        dc.PushOpacity(.28);
        Paint.Sparkline(dc, new Rect(r.Right - 70, r.Bottom - 27, 54, 15), history,
            dark ? Color.FromRgb(151, 189, 223) : Color.FromRgb(78, 117, 155));
        dc.Pop();
    }
}
