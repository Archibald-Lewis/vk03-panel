using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace VK03Panel.Core.Themes;

/// <summary>Luminous instrument panel with calibrated arcs and real load history.</summary>
public sealed class NebulaTheme : IPanelTheme
{
    private static readonly FontFamily Face = new("Bahnschrift, Segoe UI, Microsoft YaHei UI");
    private static readonly Rect Volume = new(316, 280, 284, 48);
    private static readonly Rect Slider = new(324, 302, 244, 4);
    public string Id => "nebula";
    public string DisplayName => "星环";
    public string Description => "发光环形仪表、负载峰值与切角控制台";
    public Size DesignSize => new(960, 360);
    public double SliderLeft => Slider.Left;
    public double SliderWidth => Slider.Width;
    public Rect Bounds(PanelAction action) => action switch
    {
        PanelAction.CycleTheme => new Rect(164, 108, 56, 48),
        PanelAction.Mute => new Rect(256, 280, 52, 48),
        PanelAction.Volume => Volume,
        PanelAction.Previous => new Rect(32, 272, 56, 56),
        PanelAction.Play => new Rect(96, 272, 56, 56),
        PanelAction.Next => new Rect(160, 272, 56, 56),
        PanelAction.Lock => new Rect(624, 264, 312, 72),
        _ => Rect.Empty,
    };
    public PanelAction? HitTest(Point p)
    {
        foreach (var a in Enum.GetValues<PanelAction>())
            if (!Bounds(a).IsEmpty && Bounds(a).Contains(p)) return a;
        return null;
    }

    public void Draw(PanelViewModel m, DrawingContext dc, Size size)
    {
        var dark = m.DarkMode;
        var cyan = dark ? Color.FromRgb(71, 223, 232) : Color.FromRgb(0, 119, 145);
        var gold = dark ? Color.FromRgb(244, 184, 100) : Color.FromRgb(151, 88, 18);
        var violet = dark ? Color.FromRgb(195, 156, 255) : Color.FromRgb(113, 62, 179);
        var ink = Paint.Brush(dark ? Color.FromRgb(235, 245, 248) : Color.FromRgb(23, 42, 55));
        var muted = Paint.Brush(dark ? Color.FromRgb(151, 173, 184) : Color.FromRgb(78, 101, 115));
        var faint = Paint.Brush(Paint.WithAlpha(cyan, dark ? .12 : .14));
        dc.DrawRectangle(Paint.Brush(dark ? Color.FromRgb(5, 12, 18) : Color.FromRgb(232, 241, 244)), null, new Rect(new Point(), size));
        Paint.RadialGlow(dc, new Point(380, 140), 330, cyan, dark ? .09 : .05);
        Paint.RadialGlow(dc, new Point(840, 125), 240, violet, dark ? .08 : .04);
        for (var x = 0; x <= 960; x += 40)
            dc.DrawLine(Paint.Stroke(Paint.Brush(Paint.WithAlpha(cyan, .035)), 1), new Point(x, 0), new Point(x, 360));
        for (var y = 0; y <= 360; y += 40)
            dc.DrawLine(Paint.Stroke(Paint.Brush(Paint.WithAlpha(cyan, .035)), 1), new Point(0, y), new Point(960, y));
        Frame(dc, new Rect(24, 20, 200, 136), cyan, dark);
        Frame(dc, new Rect(24, 168, 200, 168), cyan, dark);
        Frame(dc, new Rect(532, 20, 180, 228), gold, dark);
        Frame(dc, new Rect(728, 20, 212, 228), violet, dark);
        Frame(dc, new Rect(248, 264, 360, 72), cyan, dark);
        Frame(dc, Bounds(PanelAction.Lock), gold, dark);
        foreach (var a in Enum.GetValues<PanelAction>())
            if (m.PressedAction == a && !Bounds(a).IsEmpty)
                dc.DrawRectangle(Paint.Brush(Paint.WithAlpha(cyan, .16)), null, Rect.Inflate(Bounds(a), -2, -2));

        T(dc, m.Time.ToString("M月d日 ddd", CultureInfo.GetCultureInfo("zh-CN")), 36, 32, 13, muted);
        T(dc, m.Time.ToString("HH:mm", CultureInfo.InvariantCulture), 34, 59, 45, ink, TextWeight.Medium, true);
        dc.DrawEllipse(Paint.Brush(m.PanelConnected ? cyan : gold), null, new Point(40, 128), 3, 3);
        T(dc, m.Demo ? "演示数据" : m.PanelConnected ? "USB 已连接" : "等待连接", 50, 120, 11, muted);
        T(dc, "主题 ›", 173, 122, 12, Paint.Brush(cyan));

        var cpuCenter = new Point(380, 138);
        Ring(dc, cpuCenter, 94, m.CpuLoad, cyan, dark, 42);
        C(dc, "CPU 利用率", 380, 91, 13, muted);
        C(dc, Pct(m.CpuLoad), 380, 117, 45, ink, TextWeight.Medium);
        C(dc, $"{N(m.CpuTemperatureCelsius)} °C  /  {N(m.CpuFrequencyGhz, "0.00")} GHz", 380, 177, 13, muted);
        C(dc, "近期峰值 " + Peak(m.CpuLoadHistory), 380, 231, 12, Paint.Brush(cyan));
        T(dc, "0", 295, 213, 10, muted);
        T(dc, "100%", 435, 213, 10, muted);

        T(dc, "内存", 548, 37, 14, Paint.Brush(gold), TextWeight.Medium);
        T(dc, N(m.MemoryUsedGb, "0.0"), 548, 73, 37, ink, TextWeight.Medium, true);
        T(dc, "GB", 665, 96, 13, muted);
        T(dc, $"共 {N(m.MemoryTotalGb)} GB", 548, 127, 12, muted);
        var memoryKnown = m.MemoryTotalGb is > 0 && m.MemoryUsedGb.HasValue;
        for (var i = 0; i < 16; i++)
            dc.DrawRectangle(Paint.Brush(Paint.WithAlpha(gold, memoryKnown && m.MemoryFraction * 16 > i ? .9 : .13)), null,
                new Rect(548 + i * 9.25, 156, 6, 13));
        Paint.Sparkline(dc, new Rect(548, 185, 148, 24), m.MemoryHistory, gold);
        Right(dc, memoryKnown ? Pct(m.MemoryFraction) : "—", 696, 127, 12, Paint.Brush(gold));

        Ring(dc, new Point(834, 112), 65, m.GpuLoad, violet, dark, 32);
        C(dc, "GPU", 834, 83, 13, muted);
        C(dc, Pct(m.GpuLoad), 834, 107, 31, ink, TextWeight.Medium);
        C(dc, $"{N(m.GpuTemperatureCelsius)} °C", 834, 150, 13, muted);
        C(dc, m.GpuModel.Replace("NVIDIA ", "").Replace("GeForce ", ""), 834, 195, 12, muted, maxWidth: 180);
        C(dc, "近期峰值 " + Peak(m.GpuLoadHistory), 834, 213, 10, Paint.Brush(violet));

        var art = new Rect(36, 181, 40, 40);
        if (m.Media.Artwork is not null) Paint.ClipRounded(dc, art, 3, () => dc.DrawImage(m.Media.Artwork, art));
        else { dc.DrawRectangle(faint, null, art); MacGlyphs.MusicNote(dc, Box(56, 201, 23), Paint.Brush(cyan)); }
        T(dc, string.IsNullOrEmpty(m.Media.Title) ? "暂无播放" : m.Media.Title, 88, 183, 14, ink, TextWeight.Medium, maxWidth: 124);
        T(dc, string.IsNullOrEmpty(m.Media.Artist) ? "等待媒体播放" : m.Media.Artist, 88, 207, 11, muted, maxWidth: 124);
        Paint.Bar(dc, new Rect(36, 240, 176, 3), m.Media.Progress, faint, Paint.Brush(cyan));
        T(dc, Time(m.Media.ElapsedSeconds), 36, 251, 10, muted, tabular: true);
        Right(dc, Time(m.Media.DurationSeconds), 212, 251, 10, muted);
        Win11Glyphs.Previous(dc, Box(60, 300, 23), ink);
        Win11Glyphs.Next(dc, Box(188, 300, 23), ink);
        dc.DrawEllipse(Paint.Brush(Paint.WithAlpha(cyan, .12)), Paint.Stroke(Paint.Brush(cyan), 1), new Point(124, 300), 24, 24);
        if (m.Media.Playing) MacGlyphs.Pause(dc, Box(124, 300, 23), Paint.Brush(cyan));
        else MacGlyphs.Play(dc, Box(124, 300, 23), Paint.Brush(cyan));

        T(dc, m.Muted ? "已静音" : "音量", 324, 275, 11, muted);
        Right(dc, Pct(m.Volume), 584, 274, 12, Paint.Brush(cyan));
        Win11Glyphs.Speaker(dc, Box(282, 304, 24), Paint.Brush(cyan), m.Muted);
        Paint.Bar(dc, Slider, m.Muted ? 0 : m.Volume ?? 0, faint, Paint.Brush(cyan));
        if (m.Volume.HasValue)
        {
            var x = Slider.X + (m.Muted ? 0 : Math.Clamp(m.Volume.Value, 0, 1)) * Slider.Width;
            dc.DrawRectangle(Paint.Brush(cyan), null, new Rect(x - 2, 296, 4, 16));
        }
        Win11Glyphs.Lock(dc, Box(654, 300, 24), Paint.Brush(gold));
        T(dc, m.LockProgress > 0 ? "保持按住" : "按住锁屏", 682, 291, 17, ink, TextWeight.Medium);
        T(dc, "1.2 秒", 850, 293, 12, muted);
        if (m.LockProgress > 0) dc.DrawRectangle(Paint.Brush(gold), null, new Rect(640, 328, 280 * Math.Clamp(m.LockProgress, 0, 1), 2));
    }

    private static void Ring(DrawingContext dc, Point c, double radius, double? fraction, Color color, bool dark, int ticks)
    {
        var value = Math.Clamp(fraction ?? 0, 0, 1);
        Arc(dc, c, radius, 135, 405, Paint.Stroke(Paint.Brush(Paint.WithAlpha(color, .14)), 3));
        if (value > 0)
        {
            Arc(dc, c, radius, 135, 135 + 270 * value, Paint.Stroke(Paint.Brush(Paint.WithAlpha(color, .07)), 17));
            Arc(dc, c, radius, 135, 135 + 270 * value, Paint.Stroke(Paint.Brush(Paint.WithAlpha(color, .22)), 7));
            Arc(dc, c, radius, 135, 135 + 270 * value, Paint.Stroke(Paint.Brush(color), 3));
        }
        Arc(dc, c, radius - 8, 135, 405, Paint.Stroke(Paint.Brush(Paint.WithAlpha(color, .12)), .7));
        for (var i = 0; i <= ticks; i++)
        {
            var angle = 135 + 270.0 * i / ticks;
            var fill = fraction.HasValue && i / (double)ticks <= value;
            dc.DrawLine(Paint.Stroke(Paint.Brush(Paint.WithAlpha(color, fill ? .85 : .22)), i % 5 == 0 ? 1.5 : 1),
                At(c, radius + 7, angle), At(c, radius + (i % 5 == 0 ? 15 : 11), angle));
        }
    }
    private static void Arc(DrawingContext dc, Point c, double r, double start, double end, Pen pen)
    {
        var f = new PathFigure { StartPoint = At(c, r, start), IsClosed = false, IsFilled = false };
        f.Segments.Add(new ArcSegment(At(c, r, end), new Size(r, r), 0, end - start > 180, SweepDirection.Clockwise, true));
        var g = new PathGeometry(new[] { f }); g.Freeze(); dc.DrawGeometry(null, pen, g);
    }
    private static Point At(Point c, double r, double angle) => new(c.X + Math.Cos(angle * Math.PI / 180) * r, c.Y + Math.Sin(angle * Math.PI / 180) * r);
    private static void Frame(DrawingContext dc, Rect r, Color color, bool dark)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(r.Left + 10, r.Top), true, true);
            c.PolyLineTo(new[] { new Point(r.Right, r.Top), new Point(r.Right, r.Bottom - 10), new Point(r.Right - 10, r.Bottom),
                new Point(r.Left, r.Bottom), new Point(r.Left, r.Top + 10) }, true, false);
        }
        g.Freeze();
        dc.DrawGeometry(Paint.Brush(dark ? Color.FromArgb(145, 9, 18, 27) : Color.FromArgb(155, 255, 255, 255)), Paint.Stroke(Paint.Brush(Paint.WithAlpha(color, .24)), 1), g);
        dc.DrawLine(Paint.Stroke(Paint.Brush(color), 2), new Point(r.Left + 12, r.Top), new Point(r.Left + 44, r.Top));
    }
    private static Rect Box(double x, double y, double s) => new(x - s / 2, y - s / 2, s, s);
    private static string N(double? n, string format = "0") => n?.ToString(format, CultureInfo.InvariantCulture) ?? "—";
    private static string Pct(double? n) => n.HasValue ? N(n * 100) + "%" : "—";
    private static string Peak(IReadOnlyList<double> h) => h.Count > 0 ? Pct(h.Max()) : "—";
    private static string Time(double s) => $"{(int)Math.Max(0, s) / 60}:{(int)Math.Max(0, s) % 60:00}";
    private static void T(DrawingContext dc, string s, double x, double y, double size, Brush b, TextWeight w = TextWeight.Regular, bool tabular = false, double? maxWidth = null)
        => Paint.Text(dc, s, x, y, size, b, w, tabular, maxWidth, Face);
    private static void C(DrawingContext dc, string s, double x, double y, double size, Brush b, TextWeight w = TextWeight.Regular, double? maxWidth = null)
    {
        if (maxWidth.HasValue && Paint.MeasureText(s, size, w, family: Face) > maxWidth.Value)
            T(dc, s, x - maxWidth.Value / 2, y, size, b, w, maxWidth: maxWidth);
        else Paint.CenteredText(dc, s, x, y, size, b, w, family: Face);
    }
    private static void Right(DrawingContext dc, string s, double right, double y, double size, Brush b)
        => T(dc, s, right - Paint.MeasureText(s, size, tabular: true, family: Face), y, size, b, tabular: true);
}
