using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VK03Panel.Core.Themes;
using Windows.Media.Control;

namespace VK03Panel.Core.Media;

/// <summary>
/// Now-playing data from the Windows Global System Media Transport Controls session manager,
/// the platform replacement for the macOS MediaRemote helper.
/// </summary>
public sealed class MediaWatcher : IDisposable
{
    private readonly object _sync = new();
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private PanelMediaState _state = PanelMediaState.Empty;
    private TimeSpan _position;
    private TimeSpan _end;
    private DateTimeOffset _timelineCapturedAt = DateTimeOffset.UtcNow;
    private bool _playing;
    private bool _disposed;

    /// <summary>Raised on thread-pool threads whenever the session or its metadata changes.</summary>
    public event Action? Updated;

    public PanelMediaState Snapshot()
    {
        lock (_sync)
        {
            var elapsed = _position;
            if (_playing)
            {
                elapsed += DateTimeOffset.UtcNow - _timelineCapturedAt;
            }

            var duration = _end > TimeSpan.Zero ? _end.TotalSeconds : 0;
            var elapsedSeconds = Math.Clamp(elapsed.TotalSeconds, 0, duration > 0 ? duration : double.MaxValue);

            return new PanelMediaState
            {
                Title = _state.Title,
                Artist = _state.Artist,
                Album = _state.Album,
                SourceApp = _state.SourceApp,
                Playing = _playing,
                DurationSeconds = duration,
                ElapsedSeconds = elapsedSeconds,
                Artwork = _state.Artwork,
                HasSession = _state.HasSession,
            };
        }
    }

    public async Task<bool> InitializeAsync()
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_disposed)
            {
                return false;
            }

            _manager = manager;
            manager.CurrentSessionChanged += OnCurrentSessionChanged;
            Attach(manager.GetCurrentSession());
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        Attach(sender.GetCurrentSession());
    }

    private void Attach(GlobalSystemMediaTransportControlsSession? session)
    {
        lock (_sync)
        {
            if (_session is not null)
            {
                _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                _session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
            }

            _session = session;
            if (session is null)
            {
                _state = PanelMediaState.Empty;
                _playing = false;
                _position = TimeSpan.Zero;
                _end = TimeSpan.Zero;
            }
            else
            {
                session.MediaPropertiesChanged += OnMediaPropertiesChanged;
                session.PlaybackInfoChanged += OnPlaybackInfoChanged;
                session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
            }
        }

        RaiseUpdated();
        _ = RefreshAsync();
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) => _ = RefreshAsync();

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) => _ = RefreshAsync();

    private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args) => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        GlobalSystemMediaTransportControlsSession? session;
        lock (_sync)
        {
            session = _session;
        }

        if (session is null)
        {
            return;
        }

        string title = string.Empty;
        string artist = string.Empty;
        string album = string.Empty;
        ImageSource? artwork = null;

        try
        {
            var properties = await session.TryGetMediaPropertiesAsync();
            title = properties?.Title ?? string.Empty;
            artist = properties?.Artist ?? string.Empty;
            album = properties?.AlbumTitle ?? string.Empty;

            if (properties?.Thumbnail is { } thumbnail)
            {
                artwork = await ReadArtworkAsync(thumbnail);
            }
        }
        catch (Exception)
        {
            // Metadata access can fail when the source app is closing; keep the previous title.
        }

        try
        {
            var playback = session.GetPlaybackInfo();
            var playing = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var timeline = session.GetTimelineProperties();
            var sourceApp = session.SourceAppUserModelId ?? string.Empty;

            lock (_sync)
            {
                _state = new PanelMediaState
                {
                    Title = title.Length > 0 ? title : _state.Title,
                    Artist = artist.Length > 0 ? artist : _state.Artist,
                    Album = album.Length > 0 ? album : _state.Album,
                    SourceApp = sourceApp,
                    Playing = playing,
                    DurationSeconds = timeline.EndTime.TotalSeconds,
                    ElapsedSeconds = timeline.Position.TotalSeconds,
                    Artwork = artwork ?? _state.Artwork,
                    HasSession = true,
                };
                _playing = playing;
                _position = timeline.Position;
                _end = timeline.EndTime;
                _timelineCapturedAt = DateTimeOffset.UtcNow;
            }
        }
        catch (Exception)
        {
            lock (_sync)
            {
                _state = new PanelMediaState
                {
                    Title = title.Length > 0 ? title : _state.Title,
                    Artist = artist.Length > 0 ? artist : _state.Artist,
                    Album = album.Length > 0 ? album : _state.Album,
                    Artwork = artwork ?? _state.Artwork,
                    HasSession = title.Length > 0,
                    Playing = _playing,
                };
            }
        }

        RaiseUpdated();
    }

    private static async Task<ImageSource?> ReadArtworkAsync(Windows.Storage.Streams.IRandomAccessStreamReference reference)
    {
        try
        {
            using var stream = await reference.OpenReadAsync();
            if (stream.Size == 0 || stream.Size > 8_000_000)
            {
                return null;
            }

            var bytes = new byte[stream.Size];
            using var reader = new Windows.Storage.Streams.DataReader(stream);
            await reader.LoadAsync((uint)stream.Size);
            reader.ReadBytes(bytes);

            using var memory = new MemoryStream(bytes, writable: false);
            var frame = BitmapFrame.Create(memory, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            frame.Freeze();
            return frame;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<bool> TogglePlayPauseAsync()
    {
        var session = GetCommandSession();
        if (session is null)
        {
            return false;
        }

        try
        {
            return await session.TryTogglePlayPauseAsync();
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> NextAsync()
    {
        var session = GetCommandSession();
        if (session is null)
        {
            return false;
        }

        try
        {
            return await session.TrySkipNextAsync();
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> PreviousAsync()
    {
        var session = GetCommandSession();
        if (session is null)
        {
            return false;
        }

        try
        {
            return await session.TrySkipPreviousAsync();
        }
        catch (Exception)
        {
            return false;
        }
    }

    private GlobalSystemMediaTransportControlsSession? GetCommandSession()
    {
        lock (_sync)
        {
            if (_session is not null)
            {
                return _session;
            }
        }

        try
        {
            return _manager?.GetCurrentSession();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void RaiseUpdated()
    {
        try
        {
            Updated?.Invoke();
        }
        catch (Exception)
        {
            // Subscribers must never break the watcher.
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _disposed = true;
            if (_manager is not null)
            {
                _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
                _manager = null;
            }

            if (_session is not null)
            {
                _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                _session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
                _session = null;
            }
        }
    }
}
