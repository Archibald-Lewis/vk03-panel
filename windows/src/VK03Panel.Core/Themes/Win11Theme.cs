using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace VK03Panel.Core.Themes;

/// <summary>960×360 hardware overview with a shared media and touch-control strip.</summary>
public sealed class Win11Theme : IPanelTheme
{
    private static readonly Rect CpuCard = new(228, 20, 228, 190);
    private static readonly Rect GpuCard = new(708, 20, 232, 190);
    private static readonly Rect MemoryCard = new(468, 20, 228, 190);
    private static readonly Rect MuteBounds = new(704, 276, 48, 48);
    private static readonly Rect VolumeBounds = new(760, 276, 176, 48);
    private static readonly Rect PreviousBounds = new(486, 250, 56, 64);
    private static readonly Rect PlayBounds = new(552, 246, 56, 72);
    private static readonly Rect NextBounds = new(618, 250, 56, 64);
    private static readonly Rect LockBounds = new(28, 198, 152, 52);
    private static readonly Rect SliderTrack = new(768, 298, 156, 4);
    private static readonly FontFamily IconFamily = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private static readonly CultureInfo Chinese = CultureInfo.GetCultureInfo("zh-CN");

    public string Id => "win11";
    public string DisplayName => "Windows 11";
    public string Description => "任务管理器性能页风格：网格曲线、硬件指标与媒体触控条";
    public Size DesignSize => new(960, 360);
    public double SliderLeft => SliderTrack.X;
    public double SliderWidth => SliderTrack.Width;

    public Rect Bounds(PanelAction action) => action switch
    {
        PanelAction.CycleTheme => new Rect(28, 48, 152, 104),
        PanelAction.Mute => MuteBounds,
        PanelAction.Volume => VolumeBounds,
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
            var bounds = Bounds(action);
            if (!bounds.IsEmpty && bounds.Contains(point)) return action;
        }
        return null;
    }

    public void Draw(PanelViewModel model, DrawingContext dc, Size size)
    {
        var dark = model.DarkMode;
        var accent = model.AccentColor ?? (dark ? Color.FromRgb(96, 205, 255) : Color.FromRgb(0, 95, 184));
        if (dark && model.AccentColor.HasValue)
            accent = Color.FromRgb((byte)(accent.R * .72 + 255 * .28), (byte)(accent.G * .72 + 255 * .28), (byte)(accent.B * .72 + 255 * .28));
        var a = Paint.Brush(accent);
        var ink = Paint.Brush(dark ? Colors.White : Color.FromRgb(26, 26, 26));
        var secondary = Paint.Brush(dark ? Color.FromRgb(198, 198, 198) : Color.FromRgb(92, 92, 92));
        var muted = Paint.Brush(dark ? Color.FromRgb(154, 154, 154) : Color.FromRgb(112, 112, 112));
        var surface = Paint.Brush(dark ? Color.FromRgb(43, 43, 43) : Colors.White);
        var track = Paint.Brush(dark ? Color.FromRgb(66, 66, 66) : Color.FromRgb(218, 218, 218));
        var divider = Paint.Stroke(track, 1);
        dc.DrawRectangle(Paint.Brush(dark ? Color.FromRgb(32, 32, 32) : Color.FromRgb(243, 243, 243)), null, new Rect(new Point(), size));

        dc.DrawRectangle(Paint.Brush(dark ? Color.FromRgb(25, 25, 25) : Colors.White), null, new Rect(208, 0, 752, 220));
        dc.DrawLine(divider, new Point(208, 20), new Point(208, 204));

        // Treat the sidebar as one centered clock/date group and a compact system action.
        Paint.CenteredText(dc, model.Time.ToString("HH:mm", CultureInfo.InvariantCulture), 104, 58, 48, ink, TextWeight.Regular, tabular: true);
        Paint.CenteredText(dc, model.Time.ToString("M月d日 ddd", Chinese), 104, 123, 14, secondary);
        dc.DrawLine(divider, new Point(40, 166), new Point(168, 166));
        var connection = model.Demo ? "演示数据" : model.PanelConnected ? "副屏已连接" : "等待连接";
        var connectionWidth = Paint.MeasureText(connection, 12);
        var connectionLeft = 104 - (connectionWidth + 13) / 2;
        dc.DrawEllipse(model.PanelConnected ? a : muted, null, new Point(connectionLeft + 3, 290), 2.5, 2.5);
        Paint.Text(dc, connection, connectionLeft + 13, 282, 12, muted);
        dc.DrawLine(divider, new Point(208, 204), new Point(208, 340));

        var cpuColor = dark ? Color.FromRgb(79, 155, 184) : Color.FromRgb(17, 125, 187);
        var memoryColor = dark ? Color.FromRgb(164, 135, 189) : Color.FromRgb(139, 87, 167);
        var gpuColor = dark ? Color.FromRgb(185, 136, 193) : Color.FromRgb(158, 77, 168);
        Metric(dc, CpuCard, "CPU", Number(model.CpuLoad * 100), "%",
            "温度", model.CpuTemperatureCelsius.HasValue ? $"{model.CpuTemperatureCelsius.Value:0} °C" : "—",
            (model.CpuFrequencyGhz.HasValue ? $"{model.CpuFrequencyGhz.Value:0.00} GHz" : "频率 —")
                + (model.PhysicalCores.HasValue ? $" · {model.PhysicalCores} 核 / {model.LogicalCores} 线程" : ""),
            model.CpuLoadHistory, dark, cpuColor, ink, secondary, muted);
        Metric(dc, MemoryCard, "内存", model.MemoryUsedGb.HasValue && model.MemoryTotalGb.HasValue ? Number(model.MemoryFraction * 100) : "—", "%",
            "已使用", model.MemoryUsedGb.HasValue ? $"{model.MemoryUsedGb.Value:0.0} GB" : "—",
            model.MemoryTotalGb.HasValue ? $"{model.MemoryTotalGb.Value:0} GB" + (string.IsNullOrEmpty(model.MemorySpeed) ? "" : $" · {model.MemorySpeed}") : "容量 —",
            model.MemoryHistory, dark, memoryColor, ink, secondary, muted);
        Metric(dc, GpuCard, "GPU", Number(model.GpuLoad * 100), "%",
            "温度", model.GpuTemperatureCelsius.HasValue ? $"{model.GpuTemperatureCelsius.Value:0} °C" : "—",
            ShortGpu(model.GpuModel), model.GpuLoadHistory, dark, gpuColor, ink, secondary, muted);

        // The media controls are a flat footer, aligned with the performance columns.
        dc.DrawRectangle(Paint.Brush(dark ? Color.FromRgb(25, 25, 25) : Colors.White), null, new Rect(209, 220, 751, 140));
        dc.DrawLine(divider, new Point(228, 226), new Point(936, 226));
        dc.DrawLine(divider, new Point(692, 250), new Point(692, 324));

        Paint.RoundedRect(dc, LockBounds, 4,
            Paint.Brush(dark ? Color.FromRgb(37, 37, 37) : Color.FromRgb(249, 249, 249)), SurfaceStroke(dark));
        if (model.PressedAction is { } pressed && pressed != PanelAction.Play)
        {
            var bounds = Bounds(pressed);
            if (!bounds.IsEmpty) Paint.RoundedRect(dc, Rect.Inflate(bounds, -1, -1), 4, Paint.Brush(Paint.WithAlpha(accent, .14)));
        }
        if (model.LockProgress > 0)
            Paint.ClipRounded(dc, LockBounds, 4, () => dc.DrawRectangle(Paint.Brush(Paint.WithAlpha(accent, .24)), null,
                new Rect(LockBounds.X, LockBounds.Y, LockBounds.Width * Math.Clamp(model.LockProgress, 0, 1), LockBounds.Height)));
        Icon(dc, "\uE72E", 55, 213, 20, model.CanLock ? secondary : muted);
        Paint.Text(dc, model.LockProgress > 0 ? "保持按住" : "按住锁屏", 77, 215, 14, model.CanLock ? ink : muted);

        var artwork = new Rect(228, 250, 64, 64);
        if (model.Media.Artwork is not null)
            Paint.ClipRounded(dc, artwork, 4, () => dc.DrawImage(model.Media.Artwork, artwork));
        else
        {
            Paint.RoundedRect(dc, artwork, 4, surface, SurfaceStroke(dark));
            Icon(dc, "\uE8D6", 260, 270, 24, muted);
        }
        Paint.Text(dc, string.IsNullOrEmpty(model.Media.Title) ? "暂无播放" : model.Media.Title, 308, 247, 16, ink, TextWeight.SemiBold, maxWidth: 160);
        Paint.Text(dc, string.IsNullOrEmpty(model.Media.Artist) ? (model.Media.HasSession ? "未知歌手" : "等待媒体播放") : model.Media.Artist,
            308, 274, 12, secondary, maxWidth: 160);
        Paint.Bar(dc, new Rect(308, 305, 160, 3), model.Media.Progress, track, a);
        if (model.Media.DurationSeconds > 0)
        {
            Paint.Text(dc, MediaTime(model.Media.ElapsedSeconds), 308, 318, 10, muted, tabular: true);
            RightText(dc, MediaTime(model.Media.DurationSeconds), 468, 318, 10, muted);
        }
        Icon(dc, "\uE892", 514, 272, 24, ink);
        Icon(dc, "\uE893", 646, 272, 24, ink);
        dc.DrawEllipse(model.PressedAction == PanelAction.Play ? Paint.Brush(Paint.WithAlpha(accent, .65)) : a,
            null, new Point(580, 282), 24, 24);
        Icon(dc, model.Media.Playing ? "\uE769" : "\uE768", 580, 270, 26, Paint.Brush(ContrastText(accent)));

        Paint.Text(dc, model.Muted ? "已静音" : "音量", 716, 249, 12, secondary);
        RightText(dc, model.Volume.HasValue ? Percent(model.Volume) : "—", 932, 246, 18, ink);
        Icon(dc, model.Muted ? "\uE74F" : "\uE767", 728, 289, 22, model.Muted ? secondary : ink);
        Paint.Bar(dc, SliderTrack, model.Muted ? 0 : model.Volume ?? 0, track, a);
        if (model.Volume.HasValue)
        {
            var knobX = SliderTrack.X + (model.Muted ? 0 : Math.Clamp(model.Volume.Value, 0, 1)) * SliderTrack.Width;
            dc.DrawEllipse(Paint.Brush(dark ? Color.FromRgb(230, 230, 230) : Colors.White),
                Paint.Stroke(Paint.Brush(Color.FromArgb(24, 0, 0, 0)), 1), new Point(knobX, 300), 9, 9);
            dc.DrawEllipse(a, null, new Point(knobX, 300), 4.5, 4.5);
        }
    }

    private static string MediaTime(double seconds)
    {
        var value = (int)Math.Max(0, seconds);
        return $"{value / 60}:{value % 60:00}";
    }

    private static void Metric(DrawingContext dc, Rect card, string title, string value, string unit,
        string sideLabel, string sideValue, string detail, IReadOnlyList<double> history,
        bool dark, Color color, Brush ink, Brush secondary, Brush muted)
    {
        Paint.Text(dc, title, card.X, card.Y + 2, 20, ink, TextWeight.SemiBold);
        Paint.Text(dc, detail, card.X, card.Y + 32, 12, secondary, maxWidth: card.Width - 4);
        var graph = new Rect(card.X, card.Y + 60, card.Width - 4, 68);
        PerformanceGraph(dc, graph, history, color, dark);
        Paint.Text(dc, title == "内存" ? "使用率" : "利用率", card.X, card.Y + 140, 12, secondary);
        RightText(dc, sideLabel, graph.Right, card.Y + 140, 12, secondary);
        Paint.Text(dc, value + (value == "—" ? "" : unit), card.X, card.Y + 160, 26, ink, tabular: true);
        RightText(dc, sideValue, graph.Right, card.Y + 164, 22, ink);
    }

    private static void PerformanceGraph(DrawingContext dc, Rect rect, IReadOnlyList<double> history, Color color, bool dark)
    {
        var grid = Paint.Stroke(Paint.Brush(Paint.WithAlpha(color, dark ? .18 : .13)), 1);
        dc.DrawRectangle(null, Paint.Stroke(Paint.Brush(Paint.WithAlpha(color, .6)), 1), rect);
        for (var i = 1; i < 8; i++)
            dc.DrawLine(grid, new Point(rect.Left + rect.Width * i / 8, rect.Top), new Point(rect.Left + rect.Width * i / 8, rect.Bottom));
        for (var i = 1; i < 4; i++)
            dc.DrawLine(grid, new Point(rect.Left, rect.Top + rect.Height * i / 4), new Point(rect.Right, rect.Top + rect.Height * i / 4));
        if (history.Count < 2) return;
        var points = history.Select((value, index) => new Point(rect.Left + rect.Width * index / (history.Count - 1),
            rect.Bottom - rect.Height * (double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0))).ToArray();
        var area = new StreamGeometry();
        using (var context = area.Open())
        {
            context.BeginFigure(new Point(rect.Left, rect.Bottom), true, true);
            context.PolyLineTo(points, true, false);
            context.LineTo(new Point(rect.Right, rect.Bottom), true, false);
        }
        area.Freeze();
        var trace = new StreamGeometry();
        using (var context = trace.Open())
        {
            context.BeginFigure(points[0], false, false);
            context.PolyLineTo(points.Skip(1).ToArray(), true, false);
        }
        trace.Freeze();
        dc.PushClip(new RectangleGeometry(rect));
        dc.DrawGeometry(Paint.Brush(Paint.WithAlpha(color, dark ? .22 : .13)), null, area);
        dc.DrawGeometry(null, Paint.Stroke(Paint.Brush(color), 1.2), trace);
        dc.Pop();
    }

    private static Pen SurfaceStroke(bool dark) => Paint.Stroke(
        Paint.Brush(dark ? Color.FromArgb(12, 255, 255, 255) : Color.FromArgb(14, 0, 0, 0)), 1);
    private static void Icon(DrawingContext dc, string glyph, double center, double top, double size, Brush brush)
        => Paint.CenteredText(dc, glyph, center, top, size, brush, family: IconFamily);

    private static string ShortGpu(string model)
    {
        var name = model.Replace("NVIDIA ", "", StringComparison.Ordinal).Replace("GeForce ", "", StringComparison.Ordinal).Trim();
        return string.IsNullOrEmpty(name) ? "显卡数据 —" : name;
    }
    private static string Number(double? value, string format = "0") => value?.ToString(format, CultureInfo.InvariantCulture) ?? "—";
    private static string Percent(double? value) => value.HasValue ? $"{Number(value * 100)}%" : "—";
    private static void RightText(DrawingContext dc, string text, double right, double top, double size, Brush brush)
        => Paint.Text(dc, text, right - Paint.MeasureText(text, size, TextWeight.Regular, true), top, size, brush, tabular: true);
    private static Color ContrastText(Color color) => (color.R * .299 + color.G * .587 + color.B * .114) > 155
        ? Color.FromRgb(26, 26, 26) : Colors.White;
}
