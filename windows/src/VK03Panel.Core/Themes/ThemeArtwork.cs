using System.IO;
using System.Windows.Media.Imaging;
using VK03Panel.Core.Media;

namespace VK03Panel.Core.Themes;

/// <summary>Embedded art is decoded once at panel resolution and shared as detached, frozen pixels.</summary>
internal static class ThemeArtwork
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, BitmapSource> Cache = new();

    public static BitmapSource Get(string name)
    {
        lock (Sync)
        {
            if (Cache.TryGetValue(name, out var cached)) return cached;
            using var stream = typeof(ThemeArtwork).Assembly.GetManifestResourceStream("VK03Panel.Core.ThemeArt." + name)
                ?? throw new FileNotFoundException("Missing embedded theme artwork", name);
            var decoded = new BitmapImage();
            decoded.BeginInit();
            decoded.CacheOption = BitmapCacheOption.OnLoad;
            decoded.DecodePixelWidth = 960;
            decoded.StreamSource = stream;
            decoded.EndInit();
            // Never share a decoder-backed BitmapFrame between the settings and panel STA threads.
            var detached = ArtworkFrames.Detach(decoded);
            Cache.Add(name, detached);
            return detached;
        }
    }
}
