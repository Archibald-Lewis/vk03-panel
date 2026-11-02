using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace VK03Panel.Core.Themes;

/// <summary>Artwork, quiet typography and tactile playback controls on one continuous surface.</summary>
public sealed class ImmersiveTheme : IPanelTheme, ISeekTheme
{
    private readonly object _artSync = new();
    private ImageSource? _lastArtwork;
    private BitmapSource? _softArtwork;
    private static readonly Rect Cover = new(32, 32, 296, 296);
    private static readonly Rect Progress = new(384, 230, 540, 3);
    public string Id => "immersive";
    public string DisplayName => "沉浸播放";
    public string Description => "封面氛围、舒展排版与简洁播放控制";
    public Size DesignSize => new(960, 360);
    public double SliderLeft => 736;
    public double SliderWidth => 176;
    public double SeekLeft => Progress.Left;
    public double SeekWidth => Progress.Width;
    public Rect Bounds(PanelAction a) => a switch
    {
        PanelAction.Lock => new Rect(832, 16, 104, 48),
        PanelAction.Seek => new Rect(372, 214, 564, 48),
        PanelAction.Previous => new Rect(374, 272, 64, 64),
        PanelAction.Play => new Rect(464, 264, 80, 80),
        PanelAction.Next => new Rect(570, 272, 64, 64),
        PanelAction.Mute => new Rect(660, 272, 48, 64),
        PanelAction.Volume => new Rect(724, 272, 212, 64),
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
        var ink = Paint.Brush(dark ? Color.FromRgb(246, 246, 243) : Color.FromRgb(35, 37, 36));
        var secondary = Paint.Brush(dark ? Color.FromRgb(194, 196, 192) : Color.FromRgb(85, 89, 85));
        var muted = Paint.Brush(dark ? Color.FromRgb(146, 150, 146) : Color.FromRgb(113, 118, 113));
        var track = Paint.Brush(dark ? Color.FromArgb(46, 236, 240, 233) : Color.FromArgb(48, 45, 52, 45));
        var baseColor = dark ? Color.FromRgb(22, 25, 25) : Color.FromRgb(238, 239, 233);
        var surface = new Rect(new Point(), size);
        dc.DrawRectangle(Paint.Brush(baseColor), null, surface);
        if (m.Media.Artwork is { } artwork)
        {
            dc.PushOpacity(dark ? .50 : .23);
            dc.DrawImage(SoftArtwork(artwork), surface);
            dc.Pop();
            // Keep the album's atmosphere on the left while giving text a calm, stable surface.
            Paint.LinearGradient(dc, surface, new Point(0, 0), new Point(1, 0),
                (0, Paint.WithAlpha(baseColor, .12)), (.40, Paint.WithAlpha(baseColor, .50)),
                (1, Paint.WithAlpha(baseColor, .86)));
        }
        Paint.LinearGradient(dc, surface, new Point(0, 0), new Point(0, 1),
            (0, Color.FromArgb(dark ? (byte)0 : (byte)24, 255, 255, 255)),
            (1, Color.FromArgb(dark ? (byte)45 : (byte)0, 0, 0, 0)));

        // A broad, soft shadow replaces the metallic outline and dividing rule.
        for (var i = 7; i >= 1; i--)
            Paint.RoundedRect(dc, new Rect(Cover.X - i, Cover.Y + 5 - i, Cover.Width + i * 2, Cover.Height + i * 2),
                9 + i, Paint.Brush(Color.FromArgb(dark ? (byte)5 : (byte)3, 0, 0, 0)));
        Paint.ClipRounded(dc, Cover, 8, () =>
        {
            if (m.Media.Artwork is { } image)
                dc.DrawRectangle(new ImageBrush(image) { Stretch = Stretch.UniformToFill }, null, Cover);
            else DrawRecord(dc, dark);
        });

        Paint.Text(dc, m.Time.ToString("HH:mm", CultureInfo.InvariantCulture), 384, 31, 14, muted, tabular: true);
        if (m.Demo) Paint.Text(dc, "演示数据", 438, 32, 11, muted);
        DrawMetadata(dc, m, ink, secondary, muted);

        // An absent player timeline must not leave a permanently empty progress bar.
        if (m.Media.DurationSeconds > 0)
        {
            Paint.Bar(dc, Progress, m.Media.Progress, track, secondary);
            if (m.PressedAction == PanelAction.Seek && m.Media.CanSeek)
                dc.DrawEllipse(ink, null, new Point(Progress.X + Progress.Width * m.Media.Progress, 231.5), 4, 4);
            Paint.Text(dc, Time(m.Media.ElapsedSeconds), 384, 243, 11, muted, tabular: true);
            Right(dc, Time(m.Media.DurationSeconds), 924, 243, 11, muted);
        }

        foreach (var a in new[] { PanelAction.Previous, PanelAction.Next, PanelAction.Mute, PanelAction.Volume, PanelAction.Lock })
            if (m.PressedAction == a)
                Paint.RoundedRect(dc, Rect.Inflate(Bounds(a), -2, -2), 12,
                    Paint.Brush(dark ? Color.FromArgb(22, 255, 255, 255) : Color.FromArgb(16, 0, 0, 0)));
        MacGlyphs.Previous(dc, Box(406, 304, 24), secondary);
        MacGlyphs.Next(dc, Box(602, 304, 24), secondary);
        var playFill = m.PressedAction == PanelAction.Play ? secondary : ink;
        dc.DrawEllipse(Paint.Brush(Color.FromArgb(dark ? (byte)36 : (byte)14, 0, 0, 0)), null, new Point(504, 308), 33, 33);
        dc.DrawEllipse(playFill, null, new Point(504, 304), 32, 32);
        var playInk = Paint.Brush(baseColor);
        if (m.Media.Playing) MacGlyphs.Pause(dc, Box(504, 304, 26), playInk);
        else MacGlyphs.Play(dc, Box(506, 304, 27), playInk);
        Win11Glyphs.Speaker(dc, Box(684, 305, 22), m.Muted ? muted : secondary, m.Muted);
        Paint.Bar(dc, new Rect(SliderLeft, 304, SliderWidth, 3), m.Muted ? 0 : m.Volume ?? 0, track, secondary);
        if (m.Volume.HasValue)
            dc.DrawEllipse(ink, null, new Point(SliderLeft + (m.Muted ? 0 : Math.Clamp(m.Volume.Value, 0, 1)) * SliderWidth, 305.5), 4, 4);
        // The value appears during adjustment; the idle player remains visually quiet.
        if (m.PressedAction is PanelAction.Volume or PanelAction.Mute)
            Right(dc, m.Muted ? "已静音" : m.Volume.HasValue ? $"{m.Volume.Value * 100:0}%" : "—", 912, 282, 11, muted);
        Win11Glyphs.Lock(dc, Box(850, 39, 15), m.CanLock ? muted : track);
        Paint.Text(dc, m.LockProgress > 0 ? "保持按住" : "按住锁屏", 866, 33, 11, muted);
        if (m.LockProgress > 0)
            Paint.Bar(dc, new Rect(844, 58, 80, 2), m.LockProgress, track, ink);
    }
    private static void DrawMetadata(DrawingContext dc, PanelViewModel m, Brush ink, Brush secondary, Brush muted)
    {
        var title = string.IsNullOrWhiteSpace(m.Media.Title) ? "暂无播放" : m.Media.Title;
        var formatted = new FormattedText(title, CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight,
            new Typeface(Paint.DisplayFamily, FontStyles.Normal, FontWeights.Medium, FontStretches.Normal), 38, ink, 1)
        {
            MaxTextWidth = 540, MaxLineCount = 2, LineHeight = 46, Trimming = TextTrimming.CharacterEllipsis,
        };
        // Bottom-align one or two title lines within the same reserved region.
        dc.DrawText(formatted, new Point(384, 143 - formatted.Height));
        var artist = !string.IsNullOrWhiteSpace(m.Media.Artist) ? m.Media.Artist
            : m.Media.HasSession ? "未知歌手" : m.PlaybackMessage ?? "等待媒体播放";
        Paint.Text(dc, artist, 385, 157, 18, secondary, maxWidth: 538);
        if (!string.IsNullOrWhiteSpace(m.Media.Album))
            Paint.Text(dc, m.Media.Album, 385, 188, 13, muted, maxWidth: 538);
    }
    private BitmapSource SoftArtwork(ImageSource artwork)
    {
        lock (_artSync)
        {
            if (ReferenceEquals(artwork, _lastArtwork) && _softArtwork is not null) return _softArtwork;
            var image = new Image { Source = artwork, Width = 120, Height = 45, Stretch = Stretch.UniformToFill,
                Effect = new BlurEffect { Radius = 18, RenderingBias = RenderingBias.Quality } };
            image.Measure(new Size(120, 45)); image.Arrange(new Rect(0, 0, 120, 45));
            var bitmap = new RenderTargetBitmap(120, 45, 96, 96, PixelFormats.Pbgra32); bitmap.Render(image); bitmap.Freeze();
            _lastArtwork = artwork; _softArtwork = bitmap; return bitmap;
        }
    }
    private static void DrawRecord(DrawingContext dc, bool dark)
    {
        dc.DrawRectangle(Paint.Brush(dark ? Color.FromRgb(38, 42, 42) : Color.FromRgb(219, 223, 215)), null, Cover);
        var center = new Point(180, 180);
        dc.DrawEllipse(Paint.Brush(Color.FromRgb(26, 29, 29)), null, center, 118, 118);
        for (var r = 57; r < 116; r += 5)
            dc.DrawEllipse(null, Paint.Stroke(Paint.Brush(Color.FromArgb(18, 213, 221, 214)), .7), center, r, r);
        dc.DrawEllipse(Paint.Brush(Color.FromRgb(154, 165, 152)), null, center, 42, 42);
        dc.DrawEllipse(Paint.Brush(Color.FromRgb(26, 29, 29)), null, center, 4, 4);
    }
    private static Rect Box(double x, double y, double s) => new(x - s / 2, y - s / 2, s, s);
    private static string Time(double s) { var n = (int)Math.Max(0, s); return $"{n / 60}:{n % 60:00}"; }
    private static void Right(DrawingContext dc, string s, double x, double y, double size, Brush b)
        => Paint.Text(dc, s, x - Paint.MeasureText(s, size, tabular: true), y, size, b, tabular: true);
}
