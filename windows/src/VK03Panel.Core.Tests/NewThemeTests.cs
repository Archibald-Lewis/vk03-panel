using VK03Panel.Core.Media;
using VK03Panel.Core.Config;
using VK03Panel.Core.Themes;
using VK03Panel.Core.Rendering;
using System.Windows;
using System.Windows.Media;
using System.Runtime.ExceptionServices;
using Xunit;

namespace VK03Panel.Core.Tests;

public sealed class NewThemeTests
{
    [Fact]
    public void ThemeCycleVisitsAllThemesAndReturnsToStart()
    {
        var id = PanelThemes.Win11Id;
        var visited = new HashSet<string>();
        for (var i = 0; i < PanelThemes.All.Count; i++)
        {
            Assert.True(visited.Add(id));
            var config = new PanelConfig { ThemeId = id }; config.Sanitize();
            Assert.Equal(id, config.ThemeId);
            id = PanelThemes.NextId(id);
        }
        Assert.Equal(PanelThemes.Win11Id, id);
        Assert.Equal(PanelThemes.All.Count, visited.Count);
        Assert.Equal(PanelThemes.MacId, PanelThemes.NextId("WIN11"));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(.5, 50)]
    [InlineData(1, 100)]
    [InlineData(-1, 0)]
    [InlineData(2, 100)]
    public void SeekClampsFractionAndKeepsTimelineOffset(double fraction, int offsetSeconds)
    {
        Assert.True(PlaybackSeek.TryGetTicks(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(110), fraction, out var ticks));
        Assert.Equal(TimeSpan.FromSeconds(10 + offsetSeconds).Ticks, ticks);
    }
    [Fact]
    public void SeekRejectsMissingTimelineAndInvalidNumbers()
    {
        Assert.False(PlaybackSeek.TryGetTicks(TimeSpan.Zero, TimeSpan.Zero, .5, out _));
        Assert.False(PlaybackSeek.TryGetTicks(TimeSpan.Zero, TimeSpan.FromSeconds(10), double.NaN, out _));
        Assert.False(PlaybackSeek.TryGetTicks(TimeSpan.Zero, TimeSpan.FromSeconds(10), double.PositiveInfinity, out _));
    }
    [Fact]
    public void SeekEndpointsStayInsideTheirTouchTarget()
    {
        var theme = PanelThemes.Immersive;
        var seek = (ISeekTheme)theme;
        var bounds = theme.Bounds(PanelAction.Seek);
        Assert.Equal(PanelAction.Seek, theme.HitTest(new Point(seek.SeekLeft, bounds.Y + bounds.Height / 2)));
        Assert.Equal(PanelAction.Seek, theme.HitTest(new Point(seek.SeekLeft + seek.SeekWidth, bounds.Y + bounds.Height / 2)));
    }

    [Theory]
    [InlineData(PanelThemes.NebulaId, true)]
    [InlineData(PanelThemes.NebulaId, false)]
    [InlineData(PanelThemes.ImmersiveId, true)]
    [InlineData(PanelThemes.ImmersiveId, false)]
    [InlineData(PanelThemes.SketchId, true)]
    [InlineData(PanelThemes.SketchId, false)]
    [InlineData(PanelThemes.AnimeId, true)]
    [InlineData(PanelThemes.AnimeId, false)]
    public void NewThemesRenderWithMissingDataAndWithArtwork(string id, bool dark)
    {
        OnSta(() =>
        {
            var theme = PanelThemes.Get(id);
            var empty = PanelRenderer.Render(theme, new PanelViewModel { DarkMode = dark }, 90);
            Assert.Equal(960 * 360 * 4, empty.Bgra.Length);
            var artwork = SolidArtwork(Colors.Coral);
            var model = new PanelViewModel
            {
                DarkMode = dark, CpuLoad = 1, CpuTemperatureCelsius = 100, GpuLoad = 1,
                MemoryUsedGb = 1024, MemoryTotalGb = 2048, Volume = 1,
                Media = new PanelMediaState { HasSession = true, CanSeek = true, Title = new string('曲', 150),
                    Artist = new string('歌', 150), Album = new string('专', 150), Artwork = artwork, DurationSeconds = 300, ElapsedSeconds = 100 },
            };
            var frame = PanelRenderer.RenderEncoded(theme, model, 90);
            Assert.Equal(960 * 360 * 3, frame.Bgr.Length);
            var rotated = PanelRenderer.Render(theme, model, 0);
            Assert.Equal(360, rotated.Width); Assert.Equal(960, rotated.Height);
        });
    }
    [Fact]
    public void ImmersiveCoverUpdatesWhenTheTrackArtworkChanges()
    {
        OnSta(() =>
        {
            var theme = new ImmersiveTheme();
            var red = PanelRenderer.Render(theme, new PanelViewModel { Media = new PanelMediaState { Artwork = SolidArtwork(Colors.Red) } }, 90);
            var green = PanelRenderer.Render(theme, new PanelViewModel { Media = new PanelMediaState { Artwork = SolidArtwork(Colors.Lime) } }, 90);
            var offset = (180 * 960 + 180) * 4;
            Assert.Equal(255, red.Bgra[offset + 2]); Assert.Equal(0, red.Bgra[offset + 1]);
            Assert.Equal(0, green.Bgra[offset + 2]); Assert.Equal(255, green.Bgra[offset + 1]);
        });
    }
    [Fact]
    public void DecodedBitmapArtworkCanMoveBetweenLoaderAndRenderThreads()
    {
        System.Windows.Media.Imaging.BitmapSource? shared = null;
        OnSta(() =>
        {
            var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32,
                null, new byte[] { 0,0,255,255, 0,0,255,255, 0,0,255,255, 0,0,255,255 }, 8);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using var stream = new System.IO.MemoryStream(); encoder.Save(stream); stream.Position = 0;
            var decoded = System.Windows.Media.Imaging.BitmapFrame.Create(stream,
                System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
            shared = ArtworkFrames.Detach(decoded);
        });
        OnSta(() =>
        {
            Assert.NotNull(shared); Assert.True(shared.IsFrozen);
            // Freezing an ImageBrush around a raw BitmapFrame used to revisit its
            // loader-thread decoder and freeze the physical panel's render loop.
            var brush = new ImageBrush(shared); brush.Freeze();
            var frame = PanelRenderer.Render(new ImmersiveTheme(), new PanelViewModel
                { Media = new PanelMediaState { Artwork = shared, HasSession = true } }, 90);
            Assert.Equal(255, frame.Bgra[(180 * 960 + 180) * 4 + 2]);
        });
    }

    [Fact]
    public void EmbeddedThemeArtIsDetachedBeforeSharingBetweenSettingsAndWorkerThreads()
    {
        var names = new[] { "nebula-space.png", "sketch-cat-light.png", "sketch-cat-dark.png", "anime-night.png" };
        OnSta(() =>
        {
            foreach (var name in names)
            {
                var art = ThemeArtwork.Get(name);
                Assert.True(art.IsFrozen);
                Assert.Equal(960, art.PixelWidth);
                Assert.Equal(360, art.PixelHeight);
            }
        });
        OnSta(() =>
        {
            foreach (var name in names)
            {
                var brush = new ImageBrush(ThemeArtwork.Get(name));
                brush.Freeze(); // Decoder affinity used to fail specifically at this step.
            }
            foreach (var theme in new[] { PanelThemes.Nebula, PanelThemes.Sketch, PanelThemes.Anime })
                foreach (var rotation in new[] { 0, 90, 180, 270 })
                    Assert.Equal(960 * 360 * 3, PanelRenderer.RenderEncoded(theme, new PanelViewModel(), rotation).Bgr.Length);
        });
    }

    private static ImageSource SolidArtwork(Color color)
    {
        var group = new DrawingGroup();
        using (var dc = group.Open()) dc.DrawRectangle(new SolidColorBrush(color), null, new Rect(0, 0, 256, 256));
        var art = new DrawingImage(group); art.Freeze(); return art;
    }
    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Theme render timed out");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
