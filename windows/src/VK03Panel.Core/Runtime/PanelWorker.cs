using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using VK03Panel.Core.Audio;
using VK03Panel.Core.Config;
using VK03Panel.Core.Media;
using VK03Panel.Core.Metrics;
using VK03Panel.Core.Platform;
using VK03Panel.Core.Rendering;
using VK03Panel.Core.Themes;
using VK03Panel.Core.Touch;
using VK03Panel.Core.Transport;

namespace VK03Panel.Core.Runtime;

/// <summary>Snapshot of the worker state consumed by the settings window and tray.</summary>
public sealed class PanelWorkerStatus
{
    public bool PanelConnected { get; init; }

    public string DeviceSummary { get; init; } = string.Empty;

    public string TransportMessage { get; init; } = "等待副屏";

    public string DataSource { get; init; } = string.Empty;

    public string TemperatureSource { get; init; } = string.Empty;

    public string MediaTitle { get; init; } = string.Empty;

    public bool MediaPlaying { get; init; }

    public double? Volume { get; init; }

    public bool Muted { get; init; }

    public long FramesSent { get; init; }

    public double LastFrameMilliseconds { get; init; }

    public string? LastError { get; init; }

    public string ThemeId { get; init; } = PanelThemes.Win11Id;

    public int Rotation { get; init; } = 90;

    public bool DarkMode { get; init; } = true;

    public MetricsSnapshot Metrics { get; init; } = MetricsSnapshot.Empty;

    public string TouchBackend { get; init; } = "等待触屏";

    public bool TouchConnected { get; init; }

    public long TouchReports { get; init; }

    public string? TouchError { get; init; }
}

/// <summary>
/// Owns the 1 Hz panel loop on a dedicated STA thread: sample, render, push, publish preview.
/// Reconnects automatically when the panel is unplugged or the vendor driver is busy.
/// </summary>
public sealed class PanelWorker : IDisposable, ITouchSink
{
    private readonly PanelConfigStore _store;
    private readonly MetricsAggregator _metrics = new();
    private readonly MediaWatcher _media = new();
    private readonly EndpointVolume _audio = new();
    private readonly TouchController _touch;
    private readonly AutoResetEvent _wake = new(false);
    private readonly object _configSync = new();

    private Thread? _thread;
    private PanelTransport? _transport;
    private PanelConfig _config;
    private DateTime _nextConnectionAttempt = DateTime.MinValue;
    private DateTime _lastDiagnosticsWrite = DateTime.MinValue;
    private volatile bool _stopping;
    private double _lockProgress;
    private PanelAction? _pressedAction;
    private long _framesSent;
    private double _lastFrameMilliseconds;
    private string? _lastError;
    private volatile bool _paused;
    private string _touchBackend = "等待触屏";
    private bool _touchConnected;
    private long _touchReports;
    private string? _touchError;
    private readonly MetricsTimeline _timeline;
    private PanelWorkerStatus _status = new();

    public PanelWorker(PanelConfigStore store)
    {
        _store = store;
        _config = store.Config.Clone();
        _timeline = new MetricsTimeline(_metrics.Sample);
        _media.Updated += RequestRefresh;
        _touch = new TouchController(
            () => ReadConfig().Rotation,
            () => PanelThemes.Get(ReadConfig().ThemeId),
            this);
    }

    public event Action<PanelWorkerStatus>? StatusChanged;

    public event Action<BitmapSource>? PreviewUpdated;

    public PanelWorkerStatus Status => _status;

    public MetricsAggregator Metrics => _metrics;

    public MediaWatcher Media => _media;

    public EndpointVolume Audio => _audio;

    public bool IsRunning => _thread is { IsAlive: true };

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _stopping = false;
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "VK03Panel.Worker",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _touch.Start();
    }

    public void Stop()
    {
        _stopping = true;
        _wake.Set();
        _thread?.Join(TimeSpan.FromSeconds(5));
        _thread = null;
        _touch.Dispose();
        _media.Dispose();
        _metrics.Dispose();
    }

    public void ApplyConfig(PanelConfig config)
    {
        lock (_configSync)
        {
            _config = config.Clone();
        }

        RequestRefresh();
    }

    public bool Paused => _paused;

    /// <summary>Re-runs hardware and sensor detection (used by the data page refresh button).</summary>
    public void RefreshSensors()
    {
        try
        {
            _metrics.Reinitialize();
        }
        catch (Exception error)
        {
            Log.Warn($"重新检测传感器失败：{error.Message}");
        }

        RequestRefresh();
    }
    public void SetPaused(bool paused)
    {
        _paused = paused;
        RequestRefresh();
    }

    public void RequestRefresh()
    {
        _wake.Set();
    }

    public void Reconnect()
    {
        _nextConnectionAttempt = DateTime.MinValue;
        _wake.Set();
    }

    public void SetPressedAction(PanelAction? action)
    {
        _pressedAction = action;
        RequestRefresh();
    }

    public void SetLockProgress(double progress)
    {
        _lockProgress = Math.Clamp(progress, 0, 1);
        RequestRefresh();
    }

    public void TouchStateChanged(string backend, bool connected, long reports, string error)
    {
        _touchBackend = string.IsNullOrEmpty(backend) ? "等待触屏" : backend;
        _touchConnected = connected;
        _touchReports = reports;
        _touchError = string.IsNullOrEmpty(error) ? null : error;
        RequestRefresh();
    }

    public void PerformAction(PanelAction action)
    {
        switch (action)
        {
            case PanelAction.Mute:
                var current = _audio.Read();
                _audio.SetMuted(!(current?.Muted ?? false));
                break;
            case PanelAction.Quieter:
                AdjustVolume(-0.0625);
                break;
            case PanelAction.Louder:
                AdjustVolume(0.0625);
                break;
            case PanelAction.Previous:
                _ = _media.PreviousAsync();
                break;
            case PanelAction.Play:
                _ = _media.TogglePlayPauseAsync();
                break;
            case PanelAction.Next:
                _ = _media.NextAsync();
                break;
            case PanelAction.Lock:
                if (LockScreen.IsAvailable)
                {
                    LockScreen.Lock();
                }

                break;
        }

        RequestRefresh();
    }

    public void SetVolumeFraction(double fraction)
    {
        _audio.SetVolume(Math.Clamp(fraction, 0, 1));
        RequestRefresh();
    }

    public void AdjustVolume(double delta)
    {
        var current = _audio.Read();
        var next = Math.Clamp((current?.Volume ?? 0) + delta, 0, 1);
        _audio.SetVolume(next);
        RequestRefresh();
    }

    private void Run()
    {
        Log.Info("面板工作线程启动");
        try
        {
            _metrics.Initialize();
            _ = Task.Run(async () =>
            {
                var ok = await _media.InitializeAsync();
                Log.Info($"GSMTC 初始化：{(ok ? "成功" : "失败")}");
                RequestRefresh();
            });

            while (!_stopping)
            {
                var cycle = Stopwatch.StartNew();
                try
                {
                    Tick();
                }
                catch (Exception error)
                {
                    _lastError = error.Message;
                    Log.Error("面板循环异常", error);
                }

                PublishStatus();
                var refreshRate = ReadConfig().RefreshPerSecond;
                var interval = TimeSpan.FromSeconds(1.0 / Math.Clamp(refreshRate, 1, 2));
                var remaining = _paused ? interval - cycle.Elapsed : _timeline.TimeUntilNextSample(refreshRate);
                if (remaining > TimeSpan.Zero)
                {
                    _wake.WaitOne(remaining);
                }
                else
                {
                    _wake.WaitOne(1);
                }

            }
        }
        finally
        {
            try
            {
                _transport?.Shutdown(ReadConfig().TurnOffPanelOnExit);
            }
            catch (Exception error)
            {
                Log.Error("关闭副屏失败", error);
            }

            _transport?.Dispose();
            _transport = null;
            Log.Info("面板工作线程退出");
        }
    }

    private void Tick()
    {
        if (_paused)
        {
            var pausedConfig = ReadConfig();
            _status = new PanelWorkerStatus
            {
                PanelConnected = _transport is { IsConnected: true },
                DeviceSummary = _transport?.Info.Summary ?? string.Empty,
                TransportMessage = "已暂停",
                DataSource = _metrics.DataSourceLabel,
                TemperatureSource = _metrics.TemperatureSourceName,
                FramesSent = _framesSent,
                LastFrameMilliseconds = _lastFrameMilliseconds,
                LastError = _lastError,
                ThemeId = pausedConfig.ThemeId,
                Rotation = pausedConfig.Rotation,
                TouchBackend = _touchBackend,
                TouchConnected = _touchConnected,
                TouchReports = _touchReports,
                TouchError = _touchError,
            };
            return;
        }

        var config = ReadConfig();
        var snapshot = _timeline.SampleIfDue(config.RefreshPerSecond);
        var audio = _audio.Read();
        var media = _media.Snapshot();
        var dark = ResolveDarkMode(config);
        var accent = ResolveAccent(config);
        var connected = _transport is { IsConnected: true };

        EnsurePanel();

        var model = PanelViewModelFactory.Create(
            snapshot,
            _metrics.Hardware,
            _metrics.GpuName,
            _metrics.DataSourceLabel,
            media,
            audio?.Volume,
            audio?.Muted ?? false,
            LockScreen.IsAvailable,
            dark,
            _transport is { IsConnected: true },
            DateTime.Now,
            demo: false,
            accent: accent,
            lockProgress: _lockProgress,
            pressedAction: _pressedAction,
            cpuLoadHistory: _timeline.CpuHistory.ToArray(),
            gpuLoadHistory: _timeline.GpuHistory.ToArray(),
            memoryHistory: _timeline.MemoryHistory.ToArray());

        var theme = PanelThemes.Get(config.ThemeId);
        var (frame, bgr) = PanelRenderer.RenderEncoded(theme, model, config.Rotation);

        if (frame.Bitmap.CanFreeze)
        {
            frame.Bitmap.Freeze();
        }

        PreviewUpdated?.Invoke(frame.Bitmap);

        if (_transport is { IsConnected: true })
        {
            try
            {
                _transport.SendFrame(bgr);
                _framesSent++;
                _lastFrameMilliseconds = _transport.LastFrameMilliseconds;
                _lastError = null;
            }
            catch (Exception error)
            {
                _lastError = error.Message;
                Log.Warn($"推帧失败：{error.Message}");
                _transport?.Dispose();
                _transport = null;
                _nextConnectionAttempt = DateTime.UtcNow.AddSeconds(2);
            }
        }

        WriteDiagnostics(config, snapshot, media, audio, frame, connected);
        _status = new PanelWorkerStatus
        {
            PanelConnected = _transport is { IsConnected: true },
            DeviceSummary = _transport?.Info.Summary ?? string.Empty,
            TransportMessage = _transport is { IsConnected: true } ? "副屏在线" : "等待副屏",
            DataSource = _metrics.DataSourceLabel,
            TemperatureSource = _metrics.TemperatureSourceName,
            MediaTitle = media.HasSession ? media.Title : "暂无播放",
            MediaPlaying = media.Playing,
            Volume = audio?.Volume,
            Muted = audio?.Muted ?? false,
            FramesSent = _framesSent,
            LastFrameMilliseconds = _lastFrameMilliseconds,
            LastError = _lastError,
            ThemeId = config.ThemeId,
            Rotation = config.Rotation,
            DarkMode = dark,
            Metrics = snapshot,
            TouchBackend = _touchBackend,
            TouchConnected = _touchConnected,
            TouchReports = _touchReports,
            TouchError = _touchError,
        };
    }

    private void EnsurePanel()
    {
        if (_transport is { IsConnected: true })
        {
            return;
        }

        if (DateTime.UtcNow < _nextConnectionAttempt)
        {
            return;
        }

        try
        {
            var transport = new PanelTransport();
            transport.Connect(message => Log.Info(message));
            _transport = transport;
            _lastError = null;
            Log.Info($"副屏已连接：{transport.Info.Summary}");
        }
        catch (Exception error)
        {
            _lastError = error.Message;
            _nextConnectionAttempt = DateTime.UtcNow.AddSeconds(2);
            _transport?.Dispose();
            _transport = null;
        }
    }

    private PanelConfig ReadConfig()
    {
        lock (_configSync)
        {
            return _config.Clone();
        }
    }

    private void PublishStatus()
    {
        try
        {
            StatusChanged?.Invoke(_status);
        }
        catch (Exception error)
        {
            Log.Error("状态回调异常", error);
        }
    }

    private static bool ResolveDarkMode(PanelConfig config)
    {
        if (config.Appearance == "dark")
        {
            return true;
        }

        if (config.Appearance == "light")
        {
            return false;
        }

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static System.Windows.Media.Color? ResolveAccent(PanelConfig config)
    {
        if (config.Accent == "fixed")
        {
            try
            {
                return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(config.AccentHex);
            }
            catch (Exception)
            {
                return null;
            }
        }

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("AccentColor") is int abgr)
            {
                var value = unchecked((uint)abgr);
                var r = (byte)(value & 0xFF);
                var g = (byte)((value >> 8) & 0xFF);
                var b = (byte)((value >> 16) & 0xFF);
                return System.Windows.Media.Color.FromRgb(r, g, b);
            }
        }
        catch (Exception)
        {
            // Fall back to theme defaults.
        }

        return null;
    }

    private void WriteDiagnostics(
        PanelConfig config,
        MetricsSnapshot snapshot,
        PanelMediaState media,
        (double? Volume, bool Muted)? audio,
        RenderedFrame frame,
        bool wasConnected)
    {
        if (DateTime.UtcNow - _lastDiagnosticsWrite < TimeSpan.FromSeconds(15))
        {
            return;
        }

        _lastDiagnosticsWrite = DateTime.UtcNow;
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            var metrics = new
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                cpuTemperature = snapshot.CpuTemperatureCelsius,
                cpuFrequencyGhz = snapshot.CpuFrequencyGhz,
                cpuLoad = snapshot.CpuLoad,
                gpuLoad = snapshot.GpuLoad,
                gpuTemperature = snapshot.GpuTemperatureCelsius,
                memoryUsedGb = snapshot.MemoryUsedGb,
                memoryTotalGb = snapshot.MemoryTotalGb,
                dataSource = _metrics.DataSourceLabel,
                temperatureSource = _metrics.TemperatureSourceName,
            };
            File.WriteAllText(AppPaths.DiagnosticsFile("metrics.json"), JsonSerializer.Serialize(metrics, JsonOptions));

            var mediaState = new
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                title = media.Title,
                artist = media.Artist,
                album = media.Album,
                playing = media.Playing,
                artwork = media.Artwork is not null,
                hasSession = media.HasSession,
            };
            File.WriteAllText(AppPaths.DiagnosticsFile("media.json"), JsonSerializer.Serialize(mediaState, JsonOptions));

            var panel = new
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                connected = _transport is { IsConnected: true },
                frames = _framesSent,
                metricsSamples = _timeline.SampleCount,
                historyPoints = _timeline.CpuHistory.Count,
                lastFrameMs = _lastFrameMilliseconds,
                lastError = _lastError,
                theme = config.ThemeId,
                rotation = config.Rotation,
                device = _transport?.Info.Summary ?? string.Empty,
                volume = audio?.Volume,
                muted = audio?.Muted,
                touchBackend = _touchBackend,
                touchConnected = _touchConnected,
                touchReports = _touchReports,
                touchError = _touchError,
            };
            File.WriteAllText(AppPaths.DiagnosticsFile("panel.json"), JsonSerializer.Serialize(panel, JsonOptions));

            PanelRenderer.SavePng(frame.Bitmap, AppPaths.DiagnosticsFile("preview.png"));

            if (_metrics.AmdSdkActive)
            {
                _metrics.TryWriteAmdDump(AppPaths.DiagnosticsFile("amd-sdk.json"));
            }
        }
        catch (Exception error)
        {
            Log.Warn($"写入诊断文件失败：{error.Message}");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public void Dispose()
    {
        Stop();
        _wake.Dispose();
    }
}
