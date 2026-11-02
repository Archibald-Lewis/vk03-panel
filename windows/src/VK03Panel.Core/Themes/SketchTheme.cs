using System.Globalization;
using System.Windows;
using System.Windows.Media;
using static VK03Panel.Core.Themes.ThemeControls;
using static VK03Panel.Core.Themes.ThemeDrawing;

namespace VK03Panel.Core.Themes;

/// <summary>Cartoon stationery: toy needle gauges, taped paper and hand-inked transport stickers.</summary>
public sealed class SketchTheme : IPanelTheme
{
    private static readonly FontFamily Face = new("Comic Sans MS, Segoe Print, Microsoft YaHei UI");
    public string Id => "sketch";
    public string DisplayName => "手绘卡通";
    public string Description => "卡通指针仪表、拼贴便签与花形播放键";
    public Size DesignSize => new(960, 360);
    public double SliderLeft => 636;
    public double SliderWidth => 184;
    public Rect Bounds(PanelAction a) => a switch
    {
        PanelAction.Previous => new Rect(318, 272, 56, 56),
        PanelAction.Play => new Rect(388, 264, 72, 72),
        PanelAction.Next => new Rect(474, 272, 56, 56),
        PanelAction.Mute => new Rect(560, 280, 48, 48),
        PanelAction.Volume => new Rect(624, 280, 208, 48),
        PanelAction.Lock => new Rect(852, 276, 72, 56),
        _ => Rect.Empty,
    };
    public PanelAction? HitTest(Point p) => Hit(this, p);
    public void Draw(PanelViewModel m, DrawingContext dc, Size size)
    {
        var dark = m.DarkMode;
        dc.DrawImage(ThemeArtwork.Get(dark ? "sketch-cat-dark.png" : "sketch-cat-light.png"), new Rect(new Point(), size));
        var ink = Paint.Brush(dark ? Color.FromRgb(233, 237, 219) : Color.FromRgb(63, 65, 51));
        var muted = Paint.Brush(dark ? Color.FromRgb(174, 190, 169) : Color.FromRgb(111, 119, 96));
        var paper = Paint.Brush(dark ? Color.FromRgb(45, 55, 47) : Color.FromRgb(255, 250, 231));
        var mint = Paint.Brush(dark ? Color.FromRgb(58, 82, 65) : Color.FromRgb(218, 237, 202));
        var peach = Paint.Brush(dark ? Color.FromRgb(88, 66, 52) : Color.FromRgb(249, 218, 187));
        var accent = Paint.Brush(dark ? Color.FromRgb(231, 188, 144) : Color.FromRgb(239, 177, 125));
        var green = Paint.Brush(dark ? Color.FromRgb(159, 199, 145) : Color.FromRgb(108, 149, 89));
        var line = Paint.Brush(dark ? Color.FromRgb(153, 174, 147) : Color.FromRgb(76, 88, 64));
        Rough(dc, new Rect(24, 24, 194, 96), paper, line, 2);
        Tape(dc, new Rect(43, 17, 51, 13), dark);
        T(dc, m.Time.ToString("HH:mm", CultureInfo.InvariantCulture), 44, 43, 36, ink, TextWeight.Bold, 154);
        Paint.CenteredText(dc, m.Time.ToString("M月d日 ddd", CultureInfo.GetCultureInfo("zh-CN")), 121, 94, 12, muted);
        Rough(dc, new Rect(24, 138, 194, 102), paper, line, 2);
        T(dc, "内存", 40, 149, 13, muted);
        T(dc, Number(m.MemoryUsedGb, "0.0") + " GB", 40, 171, 27, ink, TextWeight.Bold, 164);
        T(dc, "共 " + Number(m.MemoryTotalGb) + " GB", 41, 207, 10, muted);
        var known = m.MemoryUsedGb.HasValue && m.MemoryTotalGb is > 0;
        for (var i = 0; i < 12; i++)
        {
            var r = new Rect(40 + i * 13.3, 226, 9, 6);
            dc.DrawRoundedRectangle(known && i < m.MemoryFraction * 12 ? green : paper, Paint.Stroke(line, .8), r, 1, 1);
        }
        GaugePaper(dc, new Rect(248, 24, 176, 216), "CPU", m.CpuLoad,
            Number(m.CpuTemperatureCelsius) + " °C · " + Number(m.CpuFrequencyGhz, "0.00") + " GHz",
            mint, paper, green, ink, muted, line, dark, -.8);
        GaugePaper(dc, new Rect(444, 24, 176, 216), "GPU", m.GpuLoad,
            Number(m.GpuTemperatureCelsius) + " °C", peach, paper, accent, ink, muted, line, dark, 1);
        if (m.Demo) Paint.Text(dc, "演示数据", 645, 20, 10, muted);

        Rough(dc, new Rect(24, 258, 274, 80), paper, line, 2);
        ThemeControls.Cover(dc, m.Media, new Rect(38, 276, 44, 44), 4, mint, ink);
        Rough(dc, new Rect(35, 273, 50, 50), Brushes.Transparent, line, 1.2);
        T(dc, string.IsNullOrWhiteSpace(m.Media.Title) ? "暂无播放" : m.Media.Title, 94, 276, 16, ink, TextWeight.Bold, 187);
        Paint.Text(dc, string.IsNullOrWhiteSpace(m.Media.Artist) ? (m.Media.HasSession ? "" : m.PlaybackMessage ?? "等待媒体播放") : m.Media.Artist,
            95, 305, 11, muted, maxWidth: 185);
        Rough(dc, new Rect(616, 264, 224, 74), paper, line, 1.8);
        T(dc, "音量", 637, 270, 10, muted);
        Controls(dc, m, this, ControlDrawingStyle.Doodle, ink, accent, paper, line,
            Paint.Brush(Color.FromRgb(61, 63, 46)), false);
    }
    private static void GaugePaper(DrawingContext dc, Rect r, string label, double? fraction, string detail, Brush fill,
        Brush paper, Brush accent, Brush ink, Brush muted, Brush line, bool dark, double rotation)
    {
        dc.PushTransform(new RotateTransform(rotation, r.X + r.Width / 2, r.Y + r.Height / 2));
        Rough(dc, r, fill, line, 2.1);
        Tape(dc, new Rect(r.Left + 56, r.Top - 7, 61, 13), dark);
        T(dc, label, r.Left + 17, r.Top + 17, 15, ink, TextWeight.Bold);
        dc.DrawLine(Paint.Stroke(line, 1.1), new Point(r.Left + 16, r.Top + 40), new Point(r.Left + 58, r.Top + 41));
        var c = new Point(r.Left + r.Width / 2, r.Top + 101);
        var radius = 51.0;
        var valid = fraction.HasValue && double.IsFinite(fraction.Value);
        var value = valid ? Math.Clamp(fraction!.Value, 0, 1) : 0;
        Arc(dc, c, radius, 180, 360, Paint.Stroke(paper, 14));
        if (value > 0) Arc(dc, c, radius, 180, 180 + value * 180, Paint.Stroke(accent, 11));
        Arc(dc, c, radius + 8, 180, 360, Paint.Stroke(line, 1.4));
        for (var i = 0; i <= 10; i++)
            dc.DrawLine(Paint.Stroke(line, i % 5 == 0 ? 1.8 : 1.1), At(c, radius - 5, 180 + i * 18), At(c, radius + 4, 180 + i * 18));
        if (valid)
        {
            dc.DrawLine(Paint.Stroke(ink, 2.8), new Point(c.X, c.Y + 2), At(c, radius - 13, 180 + value * 180));
            dc.DrawEllipse(accent, Paint.Stroke(ink, 1.4), c, 5, 5);
        }
        T(dc, "0", r.Left + 27, r.Top + 113, 10, muted);
        T(dc, "100", r.Right - 45, r.Top + 113, 10, muted);
        Paint.CenteredText(dc, Percent(fraction), c.X, r.Top + 140, 30, ink, TextWeight.Bold, family: Face);
        Paint.CenteredText(dc, detail, c.X, r.Top + 187, 11, muted);
        dc.Pop();
    }
    private static void Tape(DrawingContext dc, Rect r, bool dark)
    {
        dc.DrawGeometry(Paint.Brush(dark ? Color.FromArgb(205, 161, 154, 106) : Color.FromArgb(215, 232, 208, 143)), null,
            Polygon(new Point(r.Left + 3, r.Top), new Point(r.Right, r.Top + 1), new Point(r.Right - 2, r.Bottom), new Point(r.Left, r.Bottom - 1)));
        dc.PushOpacity(.2); Hatch(dc, r, Paint.Brush(dark ? Colors.White : Colors.SaddleBrown), 5); dc.Pop();
    }
    private static void T(DrawingContext dc, string text, double x, double y, double size, Brush ink, TextWeight weight = TextWeight.Regular, double? maxWidth = null)
        => Paint.Text(dc, text, x, y, size, ink, weight, maxWidth: maxWidth, family: Face);
}
