using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace VK03Panel.Core.Themes;

/// <summary>
/// Faithful Windows port of the original macOS renderer: same 960×360 layout, same card
/// geometry, same glass palette and the same information hierarchy.
/// </summary>
public sealed class MacTheme : IPanelTheme
{
    private static readonly Rect MediaCard = new(22, 144, 208, 88);
    private static readonly Rect CpuCard = new(248, 20, 266, 212);
    private static readonly Rect MemoryCard = new(532, 20, 194, 212);
    private static readonly Rect GpuCard = new(744, 20, 194, 212);
    private static readonly Rect VolumeCard = new(22, 250, 450, 90);
    private static readonly Rect TransportCard = new(488, 250, 270, 90);
    private static readonly Rect LockCard = new(774, 250, 164, 90);
    private static readonly Rect SliderBounds = new(204, 288, 152, 10);

    private static readonly Rect MuteBounds = new(22, 250, 86, 90);
    private static readonly Rect QuieterBounds = new(108, 250, 84, 90);
    private static readonly Rect VolumeBounds = new(192, 250, 180, 90);
    private static readonly Rect LouderBounds = new(372, 250, 100, 90);
    private static readonly Rect PreviousBounds = new(488, 250, 90, 90);
    private static readonly Rect PlayBounds = new(578, 250, 90, 90);
    private static readonly Rect NextBounds = new(668, 250, 90, 90);
    private static readonly Rect LockBounds = new(774, 250, 164, 90);

    private static readonly CultureInfo Chinese = CultureInfo.GetCultureInfo("zh-CN");

    public string Id => "mac";

    public string DisplayName => "macOS";

    public string Description => "还原原版观感：玻璃卡片、青蓝强调色与 SF 风格图标";

    public Size DesignSize => new(960, 360);

    public double SliderLeft => SliderBounds.X;

    public double SliderWidth => SliderBounds.Width;

    public Rect Bounds(PanelAction action) => action switch
    {
        PanelAction.Mute => MuteBounds,
        PanelAction.Quieter => QuieterBounds,
        PanelAction.Volume => VolumeBounds,
        PanelAction.Louder => LouderBounds,
        PanelAction.Previous => PreviousBounds,
        PanelAction.Play => PlayBounds,
        PanelAction.Next => NextBounds,
        PanelAction.Lock => LockBounds,
        _ => Rect.Empty,
    };

    public PanelAction? HitTest(Point point)
    {
        foreach (var action in Enum.GetValues<PanelAction>())
        {
            if (action == PanelAction.None)
            {
                continue;
            }

            var bounds = Bounds(action);
            if (!bounds.IsEmpty && bounds.Contains(point))
            {
                return action;
            }
        }

        return null;
    }

    public void Draw(PanelViewModel model, DrawingContext dc, Size size)
    {
        var dark = model.DarkMode;
        var ink = Paint.Brush(dark ? Paint.ColorRgb(0.96, 0.97, 1) : Paint.ColorRgb(0.10, 0.14, 0.22));
        var secondary = Paint.Brush(dark ? Paint.ColorRgb(0.61, 0.68, 0.79) : Paint.ColorRgb(0.39, 0.45, 0.55));
        var accent = Paint.Brush(dark ? Paint.ColorRgb(0.31, 0.68, 1) : Paint.ColorRgb(0.02, 0.44, 0.95));
        var accentColor = dark ? Paint.ColorRgb(0.31, 0.68, 1) : Paint.ColorRgb(0.02, 0.44, 0.95);
        var cyan = Paint.Brush(dark ? Paint.ColorRgb(0.25, 0.84, 0.83) : Paint.ColorRgb(0.02, 0.60, 0.61));
        var purple = Paint.Brush(dark ? Paint.ColorRgb(0.72, 0.62, 1) : Paint.ColorRgb(0.52, 0.36, 0.91));

        DrawBackdrop(dc, size, dark, accentColor);

        var cardFill = Paint.Brush(dark ? Color.FromArgb(204, 41, 51, 71) : Color.FromArgb(173, 255, 255, 255));
        var cardStroke = Paint.Stroke(
            Paint.Brush(dark ? Color.FromArgb(31, 255, 255, 255) : Color.FromArgb(224, 255, 255, 255)),
            1);
        var track = Paint.Brush(dark ? Color.FromArgb(33, 255, 255, 255) : Color.FromArgb(33, 64, 89, 128));
        var cardShadow = Paint.Brush(dark ? Color.FromArgb(38, 0, 0, 0) : Color.FromArgb(14, 0, 0, 0));

        // Header
        MacGlyphs.Monitor(dc, SymbolBox(new Point(34, 30), 15), secondary);
        Paint.Text(dc, "VK03", 48, 20, 12, secondary, TextWeight.SemiBold);
        dc.DrawEllipse(model.HasLiveMetrics ? cyan : secondary, null, new Point(188.5, 27.5), 2.5, 2.5);
        Paint.Text(dc, model.DataSourceLabel, 196, 19, 11, secondary);

        Paint.Text(dc, model.Time.ToString("HH:mm", CultureInfo.InvariantCulture), 22, 49, 59, ink, TextWeight.Regular, tabular: true);
        Paint.Text(dc, model.Time.ToString("ss", CultureInfo.InvariantCulture), 201, 85, 16, secondary, TextWeight.Regular, tabular: true);
        Paint.Text(dc, model.Time.ToString("M月d日 dddd", Chinese), 26, 120, 13, secondary);

        // Media card
        Card(dc, MediaCard, 25, cardFill, cardStroke, cardShadow, dark);
        var artworkBounds = new Rect(34, 156, 64, 64);
        if (model.Media.Artwork is not null)
        {
            Paint.ClipRounded(dc, artworkBounds, 13, () => dc.DrawImage(model.Media.Artwork, artworkBounds));
        }
        else
        {
            Paint.RoundedRect(dc, artworkBounds, 13, Paint.Brush(Paint.WithAlpha(accentColor, 0.13)));
            MacGlyphs.MusicNote(dc, SymbolBox(new Point(66, 188), 29), accent);
        }

        Paint.Text(dc, string.IsNullOrEmpty(model.Media.Title) ? "暂无播放" : model.Media.Title, 109, 158, 14, ink, TextWeight.SemiBold, maxWidth: 108);
        Paint.Text(dc, string.IsNullOrEmpty(model.Media.Artist) ? "音乐与播客" : model.Media.Artist, 109, 182, 11, secondary, maxWidth: 108);
        Paint.Bar(dc, new Rect(110, 213, 105, 3), model.Media.Progress, track, accent);

        // CPU card
        Card(dc, CpuCard, 25, cardFill, cardStroke, cardShadow, dark);
        MacGlyphs.Cpu(dc, SymbolBox(new Point(273, 46), 16), accent);
        Paint.Text(dc, "CPU", 288, 36, 12, secondary, TextWeight.SemiBold);
        Paint.Text(dc, string.IsNullOrEmpty(model.CpuModel) ? "等待数据" : model.CpuModel, 270, 66, 20, ink, TextWeight.SemiBold, maxWidth: 224);
        if (model.PhysicalCores.HasValue && model.LogicalCores.HasValue)
        {
            Paint.Text(dc, $"{model.PhysicalCores} 核心 · {model.LogicalCores} 线程", 271, 95, 12, secondary);
        }

        Paint.Text(dc, Format(model.CpuTemperatureCelsius, "0", "—") + (model.CpuTemperatureCelsius.HasValue ? "°" : string.Empty), 267, 113, 55, ink, TextWeight.Light, tabular: true);
        Paint.Text(dc, Format(model.CpuFrequencyGhz, "0.00", "—"), 413, 123, 25, accent, TextWeight.Medium, tabular: true);
        Paint.Text(dc, "GHz", 454, 157, 12, secondary);
        Paint.Text(dc, "温度", 273, 178, 11, secondary);
        Paint.Text(dc, "负载", 271, 204, 11, secondary);
        Paint.Text(dc, FormatPercent(model.CpuLoad), 461, 202, 13, secondary, TextWeight.Medium, tabular: true);
        Paint.Bar(dc, new Rect(310, 210, 137, 5), model.CpuLoad ?? 0, track, accent);

        // Memory card
        Card(dc, MemoryCard, 25, cardFill, cardStroke, cardShadow, dark);
        MacGlyphs.Memory(dc, SymbolBox(new Point(557, 46), 16), purple);
        Paint.Text(dc, "内存", 574, 36, 12, secondary, TextWeight.SemiBold);
        Paint.Text(dc, Format(model.MemoryTotalGb, "0", "—"), 552, 69, 43, ink, TextWeight.Light, tabular: true);
        Paint.Text(dc, "GB", 618, 93, 15, secondary);
        if (!string.IsNullOrEmpty(model.MemoryModules))
        {
            Paint.Text(dc, model.MemoryModules, 555, 122, 13, secondary, TextWeight.Medium);
        }

        if (!string.IsNullOrEmpty(model.MemorySpeed))
        {
            Paint.Text(dc, model.MemorySpeed, 555, 146, 13, secondary);
        }

        Paint.Text(dc, $"已用 {Format(model.MemoryUsedGb, "0.0", "—")} GB", 555, 181, 13, ink, TextWeight.Medium);
        Paint.Bar(dc, new Rect(555, 210, 148, 5), model.MemoryFraction, track, purple);

        // GPU card
        Card(dc, GpuCard, 25, cardFill, cardStroke, cardShadow, dark);
        MacGlyphs.Display(dc, SymbolBox(new Point(769, 46), 16), cyan);
        Paint.Text(dc, "GPU", 786, 36, 12, secondary, TextWeight.SemiBold);
        var gpuModel = (model.GpuModel ?? string.Empty).Replace("NVIDIA ", string.Empty, StringComparison.Ordinal);
        Paint.Text(dc, gpuModel.Contains("GeForce", StringComparison.Ordinal) ? "GeForce" : "显卡", 767, 78, 13, secondary);
        Paint.Text(
            dc,
            string.IsNullOrEmpty(gpuModel) ? "等待数据" : gpuModel.Replace("GeForce ", string.Empty, StringComparison.Ordinal),
            765,
            102,
            23,
            ink,
            TextWeight.SemiBold,
            maxWidth: 154);
        Paint.Text(dc, FormatPercent(model.GpuLoad), 764, 143, 40, ink, TextWeight.Light, tabular: true);
        Paint.Text(dc, "利用率", 767, 189, 11, secondary);
        if (model.GpuTemperatureCelsius.HasValue)
        {
            var gpuTemperature = $"{model.GpuTemperatureCelsius.Value:0}°C";
            var temperatureWidth = Paint.MeasureText(gpuTemperature, 12, TextWeight.SemiBold);
            Paint.Text(dc, gpuTemperature, 915 - temperatureWidth, 188, 12, cyan, TextWeight.SemiBold);
        }
        Paint.Bar(dc, new Rect(767, 210, 148, 5), model.GpuLoad ?? 0, track, cyan);

        // Control band
        Card(dc, VolumeCard, 28, cardFill, cardStroke, cardShadow, dark);
        Card(dc, TransportCard, 28, cardFill, cardStroke, cardShadow, dark);
        Card(dc, LockCard, 28, cardFill, cardStroke, cardShadow, dark);

        if (model.PressedAction is { } pressed && pressed != PanelAction.None)
        {
            var highlight = Bounds(pressed);
            if (!highlight.IsEmpty)
            {
                Paint.RoundedRect(dc, Rect.Inflate(highlight, -5, -5), 23, Paint.Brush(Paint.WithAlpha(accentColor, 0.17)));
            }
        }

        MacGlyphs.Speaker(dc, SymbolBox(new Point(65, 287), 29), model.Muted ? accent : ink, model.Muted);
        Paint.Text(dc, model.Muted ? "已静音" : "静音", 49, 316, 11, secondary);

        var buttonFill = Paint.Brush(dark ? Color.FromArgb(18, 255, 255, 255) : Color.FromArgb(122, 255, 255, 255));
        Paint.RoundedRect(dc, new Rect(116, 260, 68, 68), 22, buttonFill);
        MacGlyphs.Minus(dc, SymbolBox(new Point(150, 294), 28), ink);
        Paint.Bar(dc, SliderBounds, model.Muted ? 0 : model.Volume ?? 0, track, accent);
        if (model.Volume.HasValue)
        {
            var knobX = SliderBounds.X + ((model.Muted ? 0 : Math.Clamp(model.Volume.Value, 0, 1)) * SliderBounds.Width);
            var knob = new Rect(knobX - 9, 284, 18, 18);
            Paint.RoundedRect(dc, knob, 9, Paint.Brush(dark ? Paint.ColorRgb(0.90, 0.95, 1) : Colors.White));
        }

        Paint.Text(
            dc,
            model.Volume.HasValue
                ? $"音量 {(model.Volume.Value * 100).ToString("0", CultureInfo.InvariantCulture)}%"
                : "音量 —",
            247,
            315,
            12,
            secondary,
            TextWeight.Medium,
            tabular: true);

        Paint.RoundedRect(dc, new Rect(392, 260, 68, 68), 22, buttonFill);
        MacGlyphs.Plus(dc, SymbolBox(new Point(426, 294), 28), ink);

        MacGlyphs.Previous(dc, SymbolBox(new Point(533, 294), 28), ink);
        Paint.RoundedRect(dc, new Rect(589, 260, 68, 68), 28, Paint.Brush(Paint.WithAlpha(accentColor, dark ? 0.25 : 0.13)));
        if (model.Media.Playing)
        {
            MacGlyphs.Pause(dc, SymbolBox(new Point(623, 294), 32), accent);
        }
        else
        {
            MacGlyphs.Play(dc, SymbolBox(new Point(623, 294), 32), accent);
        }

        MacGlyphs.Next(dc, SymbolBox(new Point(713, 294), 28), ink);

        if (model.LockProgress > 0)
        {
            var progress = LockBounds;
            progress.Width *= Math.Clamp(model.LockProgress, 0, 1);
            Paint.ClipRounded(dc, LockBounds, 28, () =>
            {
                dc.DrawRectangle(Paint.Brush(Paint.WithAlpha(accentColor, 0.22)), null, progress);
            });
        }

        MacGlyphs.Lock(dc, SymbolBox(new Point(856, 280), 28), model.CanLock ? ink : secondary, cardFill);
        Paint.Text(dc, "按住锁屏", 824, 313, 15, model.CanLock ? ink : secondary, TextWeight.Medium);
    }

    private static void DrawBackdrop(DrawingContext dc, Size size, bool dark, Color accentColor)
    {
        var backdrop = dark
            ? new[]
            {
                Paint.ColorRgb(0.035, 0.07, 0.13),
                Paint.ColorRgb(0.10, 0.14, 0.22),
                Paint.ColorRgb(0.045, 0.09, 0.16),
            }
            : new[]
            {
                Paint.ColorRgb(0.79, 0.88, 0.98),
                Paint.ColorRgb(0.92, 0.94, 0.98),
                Paint.ColorRgb(0.79, 0.89, 0.94),
            };

        Paint.LinearGradient(
            dc,
            new Rect(0, 0, size.Width, size.Height),
            new Point(0, 0),
            new Point(size.Width, size.Height),
            (0, backdrop[0]),
            (0.5, backdrop[1]),
            (1, backdrop[2]));

        var alpha = dark ? 0.11 : 0.09;
        Paint.RadialGlow(dc, new Point(300, -30), 410, accentColor, alpha);
        Paint.RadialGlow(dc, new Point(860, 380), 410, Paint.ColorRgb(0.25, 0.84, 0.83), alpha);
    }

    private static void Card(DrawingContext dc, Rect rect, double radius, Brush fill, Pen stroke, Brush shadow, bool dark)
    {
        var shadowRect = new Rect(rect.X, rect.Y + 3, rect.Width, rect.Height);
        var shadowWide = new Rect(rect.X, rect.Y + 7, rect.Width, rect.Height);
        Paint.RoundedRect(dc, shadowWide, radius, Paint.Brush(dark ? Color.FromArgb(18, 0, 0, 0) : Color.FromArgb(8, 15, 30, 60)));
        Paint.RoundedRect(dc, shadowRect, radius, Paint.Brush(dark ? Color.FromArgb(38, 0, 0, 0) : Color.FromArgb(14, 15, 30, 60)));
        Paint.RoundedRect(dc, rect, radius, fill);
        dc.DrawRoundedRectangle(null, stroke, rect, radius, radius);

        Paint.ClipRounded(dc, rect, radius, () =>
        {
            Paint.LinearGradient(
                dc,
                rect,
                new Point(rect.X, rect.Y),
                new Point(rect.X, rect.Bottom),
                (0, dark ? Color.FromArgb(14, 255, 255, 255) : Color.FromArgb(64, 255, 255, 255)),
                (1, Color.FromArgb(0, 255, 255, 255)));
        });
    }

    private static Rect SymbolBox(Point center, double size) =>
        new(center.X - (size / 2), center.Y - (size / 2), size, size);

    private static string Format(double? value, string format, string fallback) =>
        value.HasValue ? value.Value.ToString(format, CultureInfo.InvariantCulture) : fallback;

    private static string FormatPercent(double? value) =>
        value.HasValue ? (value.Value * 100).ToString("0", CultureInfo.InvariantCulture) + "%" : "—";
}
