using System.Globalization;
using System.Windows;
using System.Windows.Media;
using static VK03Panel.Core.Themes.ThemeControls;
using static VK03Panel.Core.Themes.ThemeDrawing;

namespace VK03Panel.Core.Themes;

/// <summary>Cut-paper anime player, vinyl sleeve and manga telemetry ribbons.</summary>
public sealed class AnimeTheme : IPanelTheme
{
    public string Id => "anime";
    public string DisplayName => "二次元";
    public string Description => "漫画斜切播放器、唱片封套与数据标签";
    public Size DesignSize => new(960, 360);
    public double SliderLeft => 506;
    public double SliderWidth => 152;
    public Rect Bounds(PanelAction a) => a switch
    {
        PanelAction.Lock => new Rect(586, 20, 104, 48),
        PanelAction.Previous => new Rect(198, 182, 52, 48),
        PanelAction.Play => new Rect(264, 174, 72, 64),
        PanelAction.Next => new Rect(350, 182, 52, 48),
        PanelAction.Mute => new Rect(432, 182, 48, 48),
        PanelAction.Volume => new Rect(494, 182, 176, 48),
        _ => Rect.Empty,
    };
    public PanelAction? HitTest(Point p) => Hit(this, p);
    public void Draw(PanelViewModel m, DrawingContext dc, Size size)
    {
        var dark = m.DarkMode;
        var bg = dark ? Color.FromRgb(15, 27, 46) : Color.FromRgb(239, 238, 240);
        var ink = Paint.Brush(dark ? Color.FromRgb(243, 240, 246) : Color.FromRgb(41, 45, 62));
        var muted = Paint.Brush(dark ? Color.FromRgb(165, 178, 197) : Color.FromRgb(100, 104, 126));
        var roseColor = dark ? Color.FromRgb(240, 177, 197) : Color.FromRgb(165, 65, 102);
        var rose = Paint.Brush(roseColor);
        var line = Paint.Brush(dark ? Color.FromRgb(117, 109, 139) : Color.FromRgb(130, 116, 142));
        var plate = Paint.Brush(dark ? Color.FromRgb(31, 44, 65) : Color.FromRgb(253, 249, 248));
        var track = Paint.Brush(dark ? Color.FromRgb(65, 78, 101) : Color.FromRgb(210, 201, 216));
        var onRose = Paint.Brush(dark ? Color.FromRgb(36, 36, 55) : Color.FromRgb(254, 244, 247));
        dc.DrawRectangle(Paint.Brush(bg), null, new Rect(new Point(), size));
        var portrait = Polygon(new Point(742, 18), new Point(940, 18), new Point(940, 342), new Point(688, 342));
        dc.PushClip(portrait); dc.DrawImage(ThemeArtwork.Get("anime-night.png"), new Rect(0, 0, 960, 360)); dc.Pop();
        dc.DrawGeometry(null, Paint.Stroke(rose, 2), portrait);
        dc.DrawLine(Paint.Stroke(line, 1), new Point(734, 18), new Point(680, 342));
        // Manga print dots live only in the margins; no decorative copy or fake visualizer.
        dc.PushOpacity(.3);
        for (var row = 0; row < 5; row++)
        for (var col = 0; col < 7; col++)
            dc.DrawEllipse(line, null, new Point(697 + col * 5 - row * .8, 24 + row * 5), 1, 1);
        dc.Pop();
        var clock = new Rect(28, 20, 164, 48);
        dc.DrawGeometry(plate, Paint.Stroke(line, 1.5), Cut(clock, 9));
        Paint.Text(dc, m.Time.ToString("HH:mm", CultureInfo.InvariantCulture), 44, 27, 29, ink, TextWeight.SemiBold, tabular: true);
        Paint.Text(dc, m.Time.ToString("M月d日 ddd", CultureInfo.GetCultureInfo("zh-CN")), 216, 39, 12, muted);
        if (m.Demo) Paint.Text(dc, "演示数据", 402, 40, 10, muted);

        var player = new Rect(24, 88, 670, 152);
        dc.DrawGeometry(plate, Paint.Stroke(line, 1.6), Cut(player, 12));
        dc.DrawGeometry(rose, null, Polygon(new Point(24, 100), new Point(32, 92), new Point(32, 119), new Point(24, 127)));
        var record = new Point(133, 163);
        dc.DrawEllipse(Paint.Brush(Color.FromRgb(22, 27, 40)), Paint.Stroke(line, 1), record, 56, 56);
        for (var r = 29; r < 55; r += 5)
            dc.DrawEllipse(null, Paint.Stroke(Paint.Brush(Color.FromArgb(60, 194, 179, 206)), .7), record, r, r);
        dc.DrawEllipse(rose, null, record, 19, 19);
        dc.DrawEllipse(plate, null, record, 3, 3);
        ThemeControls.Cover(dc, m.Media, new Rect(40, 104, 112, 112), 0, track, rose);
        dc.DrawRectangle(null, Paint.Stroke(ink, 1.2), new Rect(40, 104, 112, 112));
        dc.DrawLine(Paint.Stroke(rose, 3), new Point(40, 218), new Point(88, 218));
        Paint.Text(dc, string.IsNullOrWhiteSpace(m.Media.Title) ? "暂无播放" : m.Media.Title,
            210, 107, 29, ink, TextWeight.SemiBold, maxWidth: 462);
        Paint.Text(dc, string.IsNullOrWhiteSpace(m.Media.Artist) ? (m.Media.HasSession ? "" : m.PlaybackMessage ?? "等待媒体播放") : m.Media.Artist,
            212, 151, 14, muted, maxWidth: 458);
        Controls(dc, m, this, ControlDrawingStyle.Manga, ink, rose, plate, line,
            onRose, true);
        if (m.Media.DurationSeconds > 0)
        {
            Paint.Bar(dc, new Rect(40, 253, 632, 3), m.Media.Progress, track, rose);
            var head = 40 + 632 * m.Media.Progress;
            dc.DrawGeometry(rose, null, Polygon(new Point(head - 3, 250), new Point(head + 3, 250), new Point(head, 258)));
        }
        Ribbon(dc, new Rect(28, 278, 202, 62), "CPU", Percent(m.CpuLoad), Number(m.CpuTemperatureCelsius) + " °C",
            m.CpuLoad, ink, muted, plate, line, rose, onRose);
        Ribbon(dc, new Rect(244, 278, 204, 62), "内存", Number(m.MemoryUsedGb, "0.0") + " GB", "共 " + Number(m.MemoryTotalGb) + " GB",
            m.MemoryUsedGb.HasValue && m.MemoryTotalGb is > 0 ? m.MemoryFraction : null, ink, muted, plate, line, rose, onRose);
        Ribbon(dc, new Rect(462, 278, 226, 62), "GPU", Percent(m.GpuLoad), Number(m.GpuTemperatureCelsius) + " °C",
            m.GpuLoad, ink, muted, plate, line, rose, onRose);
    }
    private static void Ribbon(DrawingContext dc, Rect r, string name, string value, string detail, double? fraction,
        Brush ink, Brush muted, Brush plate, Brush line, Brush accent, Brush onAccent)
    {
        dc.DrawGeometry(plate, Paint.Stroke(line, 1.3), Cut(r, 9));
        dc.DrawGeometry(accent, null, Polygon(new Point(r.Left, r.Top), new Point(r.Left + 67, r.Top), new Point(r.Left + 59, r.Top + 19), new Point(r.Left, r.Top + 19)));
        Paint.Text(dc, name, r.Left + 12, r.Top + 3, 10, onAccent, TextWeight.SemiBold);
        Paint.Text(dc, detail, r.Left + 13, r.Top + 29, 10, muted, maxWidth: 79);
        Paint.Text(dc, value, r.Left + 91, r.Top + 12, 23, ink, TextWeight.SemiBold, maxWidth: r.Width - 104);
        var ratio = fraction.HasValue && double.IsFinite(fraction.Value) ? Math.Clamp(fraction.Value, 0, 1) : 0;
        var step = (r.Width - 28) / 24;
        for (var i = 0; i < 24; i++)
            dc.DrawRectangle(fraction.HasValue && i < ratio * 24 ? accent : line, null, new Rect(r.Left + 14 + i * step, r.Bottom - 10, step - 2, 3));
    }
}
