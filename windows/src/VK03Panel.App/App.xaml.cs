using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using VK03Panel.Core.Config;
using VK03Panel.Core.Platform;
using VK03Panel.Core.Runtime;
using VK03Panel.Core.Themes;

namespace VK03Panel.App;

public partial class App : System.Windows.Application
{
    private readonly SingleInstance _singleInstance;
    private readonly string[] _startupArguments;
    private readonly PanelConfigStore _store = new();
    private readonly bool _isPrimary;

    private ThemeManager? _theme;
    private PanelWorker? _worker;
    private MainWindow? _window;
    private TrayIcon? _tray;
    private bool _shuttingDown;

    public App()
    {
        _startupArguments = Environment.GetCommandLineArgs().Skip(1).ToArray();
        var waitForPrevious = _startupArguments.Contains("--elevate-wait", StringComparer.OrdinalIgnoreCase)
            ? TimeSpan.FromSeconds(12)
            : (TimeSpan?)null;
        _singleInstance = new SingleInstance(waitForPrevious);

        _isPrimary = _singleInstance.IsPrimary;
        if (_isPrimary)
        {
            _singleInstance.ActivationRequested += () => Dispatcher.Invoke(ShowSettings);
        }
        else
        {
            SingleInstance.SignalPrimary();
        }
    }

    public PanelConfigStore Store => _store;

    public PanelWorker Worker =>
        _worker ?? throw new InvalidOperationException("工作线程尚未启动。");

    public ThemeManager Theme =>
        _theme ?? throw new InvalidOperationException("主题尚未初始化。");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!_isPrimary)
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        _store.Load();
        _theme = new ThemeManager();
        _theme.Apply(_store.Config);

        _worker = new PanelWorker(_store);
        _worker.CycleThemeRequested += () => Dispatcher.BeginInvoke(() => ApplyConfig(config => config.ThemeId = PanelThemes.NextId(config.ThemeId)));
        _window = new MainWindow(_worker, _store, _theme);
        _tray = new TrayIcon(_store.Config.ThemeId, _worker.Paused);
        _tray.OpenSettingsRequested += ShowSettings;
        _tray.ExitRequested += ExitApplication;
        _tray.ThemeSelected += themeId => ApplyConfig(config => config.ThemeId = themeId);
        _tray.PausedChanged += paused => _worker.SetPaused(paused);

        _worker.Start();
        SyncStartupRegistration();

        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        var background = _startupArguments.Contains("--background", StringComparer.OrdinalIgnoreCase) ||
                         _store.Config.StartMinimized;
        if (background)
        {
            _window.Hide();
        }
        else
        {
            ShowSettings();
        }

        var pageIndex = Array.FindIndex(_startupArguments, argument => argument.Equals("--page", StringComparison.OrdinalIgnoreCase));
        if (pageIndex >= 0 && pageIndex + 1 < _startupArguments.Length)
        {
            _window.SelectPage(_startupArguments[pageIndex + 1]);
        }

        Log.Info($"应用启动（主题 {_store.Config.ThemeId}，方向 {_store.Config.Rotation}°）");
    }

    public void ShowSettings()
    {
        if (_shuttingDown || _window is null)
        {
            return;
        }

        if (!_window.IsVisible)
        {
            _window.Show();
        }

        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();
        _window.Topmost = true;
        _window.Topmost = false;
    }

    public void HideSettings()
    {
        _window?.Hide();
    }

    /// <summary>Mutates, persists and applies the configuration to the worker, theme and tray.</summary>
    public void ApplyConfig(Action<PanelConfig> mutate)
    {
        mutate(_store.Config);
        _store.Save();

        if (_worker is not null)
        {
            _worker.ApplyConfig(_store.Config);
        }

        if (_theme is not null)
        {
            _theme.Apply(_store.Config);
            _window?.ApplyTheme();
        }

        _tray?.UpdateState(_store.Config.ThemeId, _worker?.Paused ?? false);
        SyncStartupRegistration();
    }

    public void SetPaused(bool paused)
    {
        _worker?.SetPaused(paused);
        _tray?.UpdateState(_store.Config.ThemeId, paused);
    }

    public void ExitApplication()
    {
        if (_shuttingDown)
        {
            return;
        }

        _shuttingDown = true;
        try
        {
            Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _window?.CloseForExit();
            _worker?.Stop();
            _worker?.Dispose();
            _tray?.Dispose();
            _store.Save();
            Log.Info("应用退出");
        }
        finally
        {
            Shutdown();
        }
    }

    private void SyncStartupRegistration()
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable))
        {
            return;
        }

        if (_store.Config.StartWithWindows)
        {
            if (Elevation.IsElevated)
            {
                // A highest-privilege scheduled task starts the app elevated at login without UAC.
                var created = ScheduledStartup.Create(executable);
                if (created)
                {
                    StartupRegistration.SetEnabled(enabled: false, executable);
                    Log.Info("已注册管理员计划任务自启（VK03Panel）");
                }
                else
                {
                    StartupRegistration.SetEnabled(enabled: true, executable);
                    Log.Warn("注册管理员计划任务失败，回退到普通自启");
                }
            }
            else if (!ScheduledStartup.Exists())
            {
                StartupRegistration.SetEnabled(enabled: true, executable);
            }
        }
        else
        {
            StartupRegistration.SetEnabled(enabled: false, executable);
            if (Elevation.IsElevated)
            {
                ScheduledStartup.Delete();
            }
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs args)
    {
        if (args.Mode == PowerModes.Resume)
        {
            Log.Info("系统唤醒，重新连接副屏");
            _worker?.Reconnect();
        }
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs args)
    {
        if (args.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color)
        {
            Dispatcher.Invoke(() =>
            {
                _theme?.Apply(_store.Config);
                _window?.ApplyTheme();
                _worker?.RequestRefresh();
            });
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs args)
    {
        Log.Error("未处理的 UI 异常", args.Exception);
        args.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _singleInstance.Dispose();
        }
        catch (Exception)
        {
            // Ignore.
        }

        base.OnExit(e);
    }
}
