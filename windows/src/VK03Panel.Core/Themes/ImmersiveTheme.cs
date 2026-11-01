using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace VK03Panel.Core.Themes;

/// <summary>Cover-led, warm-metal now-playing page without hardware widgets.</summary>
public sealed class ImmersiveTheme : IPanelTheme, ISeekTheme
{
    private readonly object _artSync = new();
    private ImageSource? _lastArtwork;
    private BitmapSource? _softArtwork;
    private static readonly Rect Progress = new(368, 202, 568, 4);
    public string Id => "immersive";
    public string DisplayName => "沉浸播放";
    public string Description => "大封面、金属光影与完整播放控制";
    public Size DesignSize => new(960, 360);
    public double SliderLeft => 736;
    public double SliderWidth => 188;
    public double SeekLeft => Progress.Left;
    public double SeekWidth => Progress.Width;
    public Rect Bounds(PanelAction a) => a switch
    {
        PanelAction.CycleTheme => new Rect(856, 20, 80, 48),
        PanelAction.Lock => new Rect(784, 20, 56, 48),
        PanelAction.Seek => new Rect(368, 188, 568, 48),
        PanelAction.Previous => new Rect(374, 258, 64, 72),
        PanelAction.Play => new Rect(468, 256, 80, 80),
        PanelAction.Next => new Rect(578, 258, 64, 72),
        PanelAction.Mute => new Rect(666, 272, 48, 48),
        PanelAction.Volume => new Rect(724, 272, 212, 48),
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
        var gold = Paint.Brush(dark ? Color.FromRgb(220, 191, 139) : Color.FromRgb(124, 88, 38));
        var ink = Paint.Brush(dark ? Color.FromRgb(245, 241, 232) : Color.FromRgb(42, 35, 25));
        var muted = Paint.Brush(dark ? Color.FromRgb(171, 162, 145) : Color.FromRgb(119, 105, 83));
        var track = Paint.Brush(dark ? Color.FromRgb(61, 54, 43) : Color.FromRgb(213, 202, 181));
        var baseColor = dark ? Color.FromRgb(13, 12, 11) : Color.FromRgb(242, 237, 227);
        dc.DrawRectangle(Paint.Brush(baseColor), null, new Rect(new Point(), size));
        if (m.Media.Artwork is { } artwork)
        {
            dc.PushOpacity(dark ? .24 : .14);
            dc.DrawImage(SoftArtwork(artwork), new Rect(0, 0, 960, 360));
            dc.Pop();
        }
        Paint.RadialGlow(dc, new Point(180, 175), 290, Color.FromRgb(155, 116, 59), dark ? .14 : .10);
        dc.DrawLine(Paint.Stroke(Paint.Brush(dark ? Color.FromArgb(28, 220, 191, 139) : Color.FromArgb(36, 124, 88, 38)), 1), new Point(340, 40), new Point(340, 320));
        var cover = new Rect(40, 40, 280, 280);
        if (m.Media.Artwork is { } image)
        {
            Paint.RoundedRect(dc, new Rect(cover.X, cover.Y + 8, cover.Width, cover.Height), 14, Paint.Brush(Color.FromArgb(65, 0, 0, 0)));
            var brush = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
            Paint.ClipRounded(dc, cover, 14, () => dc.DrawRectangle(brush, null, cover));
            dc.DrawRoundedRectangle(null, Paint.Stroke(Paint.Brush(Color.FromArgb(45, 255, 255, 255)), 1), cover, 14, 14);
        }
        else DrawRecord(dc, new Point(180, 180), gold, dark);

        Paint.Text(dc, m.Time.ToString("HH:mm", CultureInfo.InvariantCulture), 368, 30, 13, muted, tabular: true);
        if (m.Demo) Paint.Text(dc, "演示数据", 430, 30, 12, muted);
        Paint.Text(dc, string.IsNullOrEmpty(m.Media.Title) ? "暂无播放" : m.Media.Title,
            368, 78, 34, ink, TextWeight.SemiBold, maxWidth: 568);
        Paint.Text(dc, string.IsNullOrEmpty(m.Media.Artist) ? (m.Media.HasSession ? "未知歌手" : m.PlaybackMessage ?? "等待媒体播放") : m.Media.Artist,
            369, 127, 18, muted, maxWidth: 560);
        if (!string.IsNullOrEmpty(m.Media.Album)) Paint.Text(dc, m.Media.Album, 369, 160, 12, muted, maxWidth: 560);
        Paint.Bar(dc, Progress, m.Media.Progress, track, m.Media.CanSeek ? gold : muted);
        if (m.Media.DurationSeconds > 0)
        {
            var head = Progress.X + Progress.Width * m.Media.Progress;
            dc.DrawEllipse(m.Media.CanSeek ? gold : muted, null, new Point(head, 204), 4, 4);
            Paint.Text(dc, Time(m.Media.ElapsedSeconds), 368, 219, 12, muted, tabular: true);
            Right(dc, Time(m.Media.DurationSeconds), 936, 219, 12, muted);
            Paint.CenteredText(dc, m.Media.CanSeek ? "点按进度条跳转" : "播放器未开放跳转", 652, 220, 10, muted);
        }
        foreach (var a in Enum.GetValues<PanelAction>())
            if (m.PressedAction == a && a is not (PanelAction.Play or PanelAction.Seek) && !Bounds(a).IsEmpty)
                Paint.RoundedRect(dc, Rect.Inflate(Bounds(a), -2, -2), 8, Paint.Brush(Color.FromArgb(22, 200, 170, 114)));
        MacGlyphs.Previous(dc, Box(406, 296, 26), ink);
        MacGlyphs.Next(dc, Box(610, 296, 26), ink);
        dc.DrawEllipse(m.PressedAction == PanelAction.Play ? muted : gold, null, new Point(508, 296), 34, 34);
        var playInk = Paint.Brush(dark ? Color.FromRgb(26, 22, 16) : Color.FromRgb(250, 246, 237));
        if (m.Media.Playing) MacGlyphs.Pause(dc, Box(508, 296, 29), playInk);
        else MacGlyphs.Play(dc, Box(510, 296, 29), playInk);
        Win11Glyphs.Speaker(dc, Box(690, 299, 24), m.Muted ? muted : ink, m.Muted);
        Paint.Bar(dc, new Rect(SliderLeft, 298, SliderWidth, 3), m.Muted ? 0 : m.Volume ?? 0, track, gold);
        if (m.Volume.HasValue)
            dc.DrawEllipse(gold, null, new Point(SliderLeft + (m.Muted ? 0 : Math.Clamp(m.Volume.Value, 0, 1)) * SliderWidth, 299.5), 5, 5);
        Right(dc, m.Muted ? "已静音" : m.Volume.HasValue ? $"{m.Volume.Value * 100:0}%" : "—", 924, 273, 11, muted);
        Win11Glyphs.Lock(dc, Box(812, 38, 17), m.CanLock ? muted : track);
        Paint.CenteredText(dc, m.LockProgress > 0 ? "保持按住" : "按住锁屏", 812, 57, 10, muted);
        Paint.CenteredText(dc, "切换主题", 896, 39, 12, muted);
        if (m.LockProgress > 0) dc.DrawRectangle(gold, null, new Rect(792, 72, 40 * Math.Clamp(m.LockProgress, 0, 1), 2));
    }
    private BitmapSource SoftArtwork(ImageSource artwork)
    {
        lock (_artSync)
        {
            if (ReferenceEquals(artwork, _lastArtwork) && _softArtwork is not null) return _softArtwork;
            var image = new Image { Source = artwork, Width = 120, Height = 45, Stretch = Stretch.UniformToFill,
                Effect = new BlurEffect { Radius = 16, RenderingBias = RenderingBias.Quality } };
            image.Measure(new Size(120, 45)); image.Arrange(new Rect(0, 0, 120, 45));
            var bitmap = new RenderTargetBitmap(120, 45, 96, 96, PixelFormats.Pbgra32); bitmap.Render(image); bitmap.Freeze();
            _lastArtwork = artwork; _softArtwork = bitmap; return bitmap;
        }
    }
    private static void DrawRecord(DrawingContext dc, Point c, Brush gold, bool dark)
    {
        dc.DrawEllipse(Paint.Brush(dark ? Color.FromRgb(20, 19, 18) : Color.FromRgb(43, 40, 36)), Paint.Stroke(gold, 1), c, 130, 130);
        for (var r = 59; r < 125; r += 4)
            dc.DrawEllipse(null, Paint.Stroke(Paint.Brush(Color.FromArgb(r % 3 == 0 ? (byte)35 : (byte)16, 215, 193, 149)), .6), c, r, r);
        dc.DrawEllipse(Paint.Brush(Color.FromRgb(173, 139, 84)), null, c, 49, 49);
        dc.DrawEllipse(null, Paint.Stroke(Paint.Brush(Color.FromRgb(219, 196, 156)), 1), c, 43, 43);
        MacGlyphs.MusicNote(dc, Box(c.X, c.Y - 5, 24), Paint.Brush(Color.FromRgb(47, 38, 25)));
        dc.DrawEllipse(Paint.Brush(Color.FromRgb(30, 28, 25)), null, new Point(c.X, c.Y + 18), 3, 3);
        dc.DrawEllipse(null, Paint.Stroke(Paint.Brush(Color.FromArgb(25, 255, 255, 255)), 2), c, 127, 127);
    }
    private static Rect Box(double x, double y, double s) => new(x - s / 2, y - s / 2, s, s);
    private static string Time(double s) { var n = (int)Math.Max(0, s); return $"{n / 60}:{n % 60:00}"; }
    private static void Right(DrawingContext dc, string s, double x, double y, double size, Brush b)
        => Paint.Text(dc, s, x - Paint.MeasureText(s, size, tabular: true), y, size, b, tabular: true);
}
