using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace VK03Panel.Core.Themes;

/// <summary>A compact macOS Control Center-inspired layout with neutral translucent modules.</summary>
public sealed class MacTheme : IPanelTheme
{
    private static readonly Rect ClockCard = new(20, 20, 248, 148);
    private static readonly Rect MediaCard = new(20, 184, 248, 156);
    private static readonly Rect CpuCard = new(284, 20, 208, 192);
    private static readonly Rect MemoryCard = new(508, 20, 208, 192);
    private static readonly Rect GpuCard = new(732, 20, 208, 192);
    private static readonly Rect VolumeCard = new(284, 228, 460, 112);
    private static readonly Rect LockCard = new(760, 228, 180, 112);
    private static readonly Rect MuteBounds = new(300, 272, 48, 48);
    private static readonly Rect QuieterBounds = new(352, 272, 48, 48);
    private static readonly Rect VolumeBounds = new(404, 272, 252, 48);
    private static readonly Rect LouderBounds = new(664, 272, 56, 48);
    private static readonly Rect PreviousBounds = new(40, 270, 56, 56);
    private static readonly Rect PlayBounds = new(116, 270, 56, 56);
    private static readonly Rect NextBounds = new(192, 270, 56, 56);
    private static readonly Rect SliderTrack = new(408, 280, 244, 36);
    private static readonly CultureInfo Chinese = CultureInfo.GetCultureInfo("zh-CN");
    // Use installed fonts only; Apple fonts are not bundled with the Windows app.
    private static readonly FontFamily MacFont = new("Helvetica Neue, Arial, Microsoft YaHei UI");

    public string Id => "mac";
    public string DisplayName => "macOS";
    public string Description => "控制中心风格：中性半透明模块、胶囊音量与填充播放图标";
    public Size DesignSize => new(960, 360);
    public double SliderLeft => SliderTrack.X;
    public double SliderWidth => SliderTrack.Width;
    public Rect Bounds(PanelAction action) => action switch
    {
        PanelAction.Mute => MuteBounds,
        PanelAction.Quieter => QuieterBounds,
        PanelAction.Volume => VolumeBounds,
        PanelAction.Louder => LouderBounds,
        PanelAction.Previous => PreviousBounds,
        PanelAction.Play => PlayBounds,
        PanelAction.Next => NextBounds,
        PanelAction.Lock => LockCard,
        _ => Rect.Empty,
    };
    public PanelAction? HitTest(Point point)
    {
        foreach (var action in Enum.GetValues<PanelAction>())
        {
            var bounds = Bounds(action);
            if (!bounds.IsEmpty && bounds.Contains(point)) return action;
        }
        return null;
    }

    public void Draw(PanelViewModel model, DrawingContext dc, Size size)
    {
        var dark = model.DarkMode;
        var ink = Paint.Brush(dark ? Color.FromRgb(249, 250, 251) : Color.FromRgb(30, 33, 37));
        var secondary = Paint.Brush(dark ? Color.FromRgb(198, 203, 207) : Color.FromRgb(94, 99, 105));
        var fill = Paint.Brush(dark ? Color.FromArgb(192, 55, 60, 65) : Color.FromArgb(215, 255, 255, 255));
        var line = Paint.Stroke(Paint.Brush(dark ? Color.FromArgb(42, 255, 255, 255) : Color.FromArgb(170, 255, 255, 255)), 1);
        var subtle = Paint.Brush(dark ? Color.FromArgb(26, 255, 255, 255) : Color.FromArgb(16, 0, 0, 0));
        var blue = Color.FromRgb(10, 132, 255);
        var purple = Color.FromRgb(175, 82, 222);
        var green = Color.FromRgb(48, 170, 118);
        Backdrop(dc, size, dark);
        foreach (var rect in new[] { ClockCard, MediaCard, CpuCard, MemoryCard, GpuCard, VolumeCard, LockCard })
            Glass(dc, rect, fill, line, dark);

        Center(dc, model.Time.ToString("HH:mm", CultureInfo.InvariantCulture), 144, 41, 52, ink, TextWeight.Regular, true);
        Center(dc, model.Time.ToString("M月d日 dddd", Chinese), 144, 108, 14, secondary);
        Center(dc, model.Demo ? "演示数据" : model.PanelConnected ? "已连接" : "等待连接", 144, 140, 11, secondary);

        Metric(dc, CpuCard, "CPU", Number(model.CpuTemperatureCelsius), "°C",
            model.CpuLoad.HasValue ? $"负载 {Percent(model.CpuLoad)}" : "负载 —",
            model.CpuFrequencyGhz.HasValue ? $"{model.CpuFrequencyGhz.Value:0.00} GHz" : "频率 —",
            model.CpuLoadHistory, blue, ink, secondary, MacGlyphs.Cpu, dark);
        Metric(dc, MemoryCard, "内存", Number(model.MemoryUsedGb, "0.0"), "GB",
            model.MemoryTotalGb.HasValue ? $"共 {model.MemoryTotalGb.Value:0} GB" : "容量 —",
            model.MemoryUsedGb.HasValue && model.MemoryTotalGb.HasValue ? $"已用 {Percent(model.MemoryFraction)}" : "用量 —",
            model.MemoryHistory, purple, ink, secondary, MacGlyphs.Memory, dark);
        Metric(dc, GpuCard, "GPU", Number(model.GpuLoad * 100), "%",
            ShortGpu(model.GpuModel), model.GpuTemperatureCelsius.HasValue ? $"{model.GpuTemperatureCelsius.Value:0} °C" : "温度 —",
            model.GpuLoadHistory, green, ink, secondary, MacGlyphs.Display, dark);

        if (model.PressedAction is { } pressed && pressed != PanelAction.Play)
        {
            var rect = Bounds(pressed);
            if (!rect.IsEmpty) Paint.RoundedRect(dc, Rect.Inflate(rect, -3, -3), 18, subtle);
        }
        var art = new Rect(36, 201, 52, 52);
        if (model.Media.Artwork is not null)
            Paint.ClipRounded(dc, art, 10, () => dc.DrawImage(model.Media.Artwork, art));
        else
        {
            Paint.RoundedRect(dc, art, 10, subtle);
            MacGlyphs.MusicNote(dc, Box(62, 227, 24), secondary);
        }
        Text(dc, string.IsNullOrEmpty(model.Media.Title) ? "暂无播放" : model.Media.Title,
            102, 207, 15, ink, TextWeight.SemiBold, maxWidth: 150);
        Text(dc, string.IsNullOrEmpty(model.Media.Artist) ? (model.Media.HasSession ? "未知歌手" : "等待媒体播放") : model.Media.Artist,
            102, 232, 12, secondary, maxWidth: 150);
        MacGlyphs.Previous(dc, Box(68, 297, 24), ink);
        MacGlyphs.Next(dc, Box(220, 297, 24), ink);
        dc.DrawEllipse(model.PressedAction == PanelAction.Play ? subtle : Paint.Brush(dark ? Colors.White : Color.FromRgb(40, 43, 48)),
            null, new Point(144, 297), 25, 25);
        var playInk = model.PressedAction == PanelAction.Play ? ink : Paint.Brush(dark ? Color.FromRgb(40, 43, 48) : Colors.White);
        if (model.Media.Playing) MacGlyphs.Pause(dc, Box(144, 297, 25), playInk);
        else MacGlyphs.Play(dc, Box(144, 297, 25), playInk);
        Paint.Bar(dc, new Rect(36, 262, 216, 2), model.Media.Progress, subtle, secondary);

        Text(dc, "声音", 304, 245, 14, ink, TextWeight.SemiBold);
        Right(dc, model.Muted ? "已静音" : model.Volume.HasValue ? Percent(model.Volume) : "—", 720, 245, 14, secondary);
        MacGlyphs.Speaker(dc, Box(324, 298, 24), secondary, model.Muted);
        MacGlyphs.Minus(dc, Box(376, 298, 18), secondary);
        MacGlyphs.Plus(dc, Box(692, 298, 18), secondary);
        var fraction = model.Muted ? 0 : Math.Clamp(model.Volume ?? 0, 0, 1);
        Paint.RoundedRect(dc, SliderTrack, 18, Paint.Brush(dark ? Color.FromArgb(42, 0, 0, 0) : Color.FromArgb(22, 0, 0, 0)));
        Paint.ClipRounded(dc, SliderTrack, 18, () => dc.DrawRectangle(Paint.Brush(dark ? Colors.White : Color.FromRgb(103, 110, 117)), null,
            new Rect(SliderTrack.X, SliderTrack.Y, SliderTrack.Width * fraction, SliderTrack.Height)));
        if (fraction > 0 && fraction < 1)
            dc.DrawEllipse(Paint.Brush(dark ? Color.FromRgb(232, 232, 234) : Color.FromRgb(126, 132, 138)), null,
                new Point(SliderTrack.X + SliderTrack.Width * fraction - 6, 298), 2, 7);

        var lockCircle = new Rect(826, 244, 48, 48);
        Paint.RoundedRect(dc, lockCircle, 24, subtle);
        if (model.LockProgress > 0)
            Paint.ClipRounded(dc, LockCard, 24, () => dc.DrawRectangle(subtle, null,
                new Rect(LockCard.X, LockCard.Y, LockCard.Width * Math.Clamp(model.LockProgress, 0, 1), LockCard.Height)));
        MacGlyphs.Lock(dc, Box(850, 268, 23), model.CanLock ? ink : secondary, fill);
        Center(dc, model.LockProgress > 0 ? "保持按住" : "按住锁屏", 850, 307, 14, model.CanLock ? ink : secondary, TextWeight.Medium);
    }

    private static void Metric(DrawingContext dc, Rect card, string title, string value, string unit,
        string detail, string sideDetail, IReadOnlyList<double> history, Color color, Brush ink, Brush secondary,
        Action<DrawingContext, Rect, Brush> glyph, bool dark)
    {
        dc.DrawEllipse(Paint.Brush(color), null, new Point(card.X + 29, card.Y + 29), 13, 13);
        glyph(dc, Box(card.X + 29, card.Y + 29, 16), Brushes.White);
        Text(dc, title, card.X + 51, card.Y + 20, 13, ink, TextWeight.SemiBold);
        var valueSize = 43.0;
        while (valueSize > 30 && Measure(value, valueSize, TextWeight.Regular, true) + Measure(unit, 15) + 5 > card.Width - 36) valueSize -= 2;
        Text(dc, value, card.X + 18, card.Y + 57, valueSize, ink, tabular: true);
        if (value != "—") Text(dc, unit, card.X + 22 + Measure(value, valueSize, TextWeight.Regular, true),
            card.Y + 57 + Paint.AscenderRatio * (valueSize - 15), 15, secondary);
        Text(dc, detail, card.X + 18, card.Y + 117, 12, secondary, maxWidth: 105);
        Right(dc, sideDetail, card.Right - 18, card.Y + 117, 12, secondary);
        if (history.Count >= 2) Paint.Sparkline(dc, new Rect(card.X + 18, card.Y + 148, card.Width - 36, 26), history,
            dark ? Color.FromRgb((byte)(color.R * .7 + 255 * .3), (byte)(color.G * .7 + 255 * .3), (byte)(color.B * .7 + 255 * .3)) : color);
        else dc.DrawLine(Paint.Stroke(Paint.Brush(Paint.WithAlpha(color, .2)), 1), new Point(card.X + 18, card.Y + 174), new Point(card.Right - 18, card.Y + 174));
    }

    private static void Glass(DrawingContext dc, Rect rect, Brush fill, Pen edge, bool dark)
    {
        Paint.RoundedRect(dc, new Rect(rect.X, rect.Y + 4, rect.Width, rect.Height), 24,
            Paint.Brush(Color.FromArgb(dark ? (byte)25 : (byte)12, 0, 0, 0)));
        Paint.RoundedRect(dc, rect, 24, fill, edge);
        Paint.ClipRounded(dc, rect, 24, () => Paint.LinearGradient(dc, rect, new Point(0, 0), new Point(0, 1),
            (0, Color.FromArgb(dark ? (byte)12 : (byte)50, 255, 255, 255)), (1, Color.FromArgb(0, 255, 255, 255))));
    }
    private static void Backdrop(DrawingContext dc, Size size, bool dark)
    {
        dc.DrawRectangle(Paint.Brush(dark ? Color.FromRgb(27, 35, 42) : Color.FromRgb(213, 225, 231)), null, new Rect(new Point(), size));
        Paint.RadialGlow(dc, new Point(50, 5), 520, Color.FromRgb(113, 151, 144), dark ? .25 : .16);
        Paint.RadialGlow(dc, new Point(880, 340), 500, Color.FromRgb(168, 135, 116), dark ? .18 : .16);
    }
    private static Rect Box(double x, double y, double size) => new(x - size / 2, y - size / 2, size, size);
    private static string Number(double? value, string format = "0") => value?.ToString(format, CultureInfo.InvariantCulture) ?? "—";
    private static string Percent(double? value) => value.HasValue ? $"{Number(value * 100)}%" : "—";
    private static string ShortGpu(string value) => string.IsNullOrEmpty(value) ? "等待显卡数据" : value.Replace("NVIDIA ", "", StringComparison.Ordinal).Replace("GeForce ", "", StringComparison.Ordinal);
    private static double Measure(string text, double size, TextWeight weight = TextWeight.Regular, bool tabular = false)
        => Paint.MeasureText(text, size, weight, tabular, MacFont);
    private static void Text(DrawingContext dc, string text, double x, double y, double size, Brush brush,
        TextWeight weight = TextWeight.Regular, bool tabular = false, double? maxWidth = null)
        => Paint.Text(dc, text, x, y, size, brush, weight, tabular, maxWidth, MacFont);
    private static void Center(DrawingContext dc, string text, double x, double y, double size, Brush brush,
        TextWeight weight = TextWeight.Regular, bool tabular = false)
        => Paint.CenteredText(dc, text, x, y, size, brush, weight, tabular, MacFont);
    private static void Right(DrawingContext dc, string text, double right, double y, double size, Brush brush)
        => Text(dc, text, right - Measure(text, size, tabular: true), y, size, brush, tabular: true);
}
