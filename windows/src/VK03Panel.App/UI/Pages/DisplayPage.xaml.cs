using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using VK03Panel.App;
using VK03Panel.Core.Config;
using VK03Panel.Core.Runtime;
using VK03Panel.Core.Themes;

namespace VK03Panel.App.UI.Pages;

public partial class DisplayPage : UserControl, ISettingsPage
{
    private const int DesignWidth = 960;
    private const int DesignHeight = 360;

    private readonly PanelWorker _worker;
    private readonly PanelConfigStore _store;
    private readonly ThemeManager _theme;
    private bool _initializing = true;
    private PanelAction? _pressed;
    private bool _draggingVolume;

    public DisplayPage(PanelWorker worker, PanelConfigStore store, ThemeManager theme)
    {
        _worker = worker;
        _store = store;
        _theme = theme;
        InitializeComponent();

        switch (store.Config.Rotation)
        {
            case 0:
                Rotation0.IsChecked = true;
                break;
            case 180:
                Rotation180.IsChecked = true;
                break;
            case 270:
                Rotation270.IsChecked = true;
                break;
            default:
                Rotation90.IsChecked = true;
                break;
        }

        _initializing = false;
    }

    public void UpdateStatus(PanelWorkerStatus status)
    {
        DeviceText.Text = status.PanelConnected && !string.IsNullOrEmpty(status.DeviceSummary)
            ? status.DeviceSummary
            : status.TransportMessage;
        ErrorText.Text = status.LastError ?? (status.PanelConnected
            ? "副屏工作正常；退出程序时可在“启动”页关闭屏幕。"
            : "接通副屏 USB 后会自动初始化；若厂商程序正在使用副屏，请先退出该程序。");
        FrameText.Text = status.PanelConnected
            ? $"已发送 {status.FramesSent} 帧 · {status.LastFrameMilliseconds:0.0} ms"
            : string.Empty;
    }

    public void UpdatePreview(BitmapSource? preview)
    {
        if (preview is null)
        {
            return;
        }

        PreviewImage.Source = preview;
        PreviewPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void OnRotationChecked(object sender, RoutedEventArgs e)
    {
        if (_initializing || sender is not RadioButton { Tag: string tag } || !int.TryParse(tag, out var rotation))
        {
            return;
        }

        if (_store.Config.Rotation == rotation)
        {
            return;
        }

        ((App)Application.Current).ApplyConfig(config => config.Rotation = rotation);
    }

    private void OnReconnectClicked(object sender, RoutedEventArgs e)
    {
        _worker.Reconnect();
        _worker.RefreshSensors();
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var point = ToPanelPoint(e);
        if (point is null)
        {
            return;
        }

        var theme = PanelThemes.Get(_store.Config.ThemeId);
        var action = theme.HitTest(point.Value);
        if (action is null)
        {
            return;
        }

        _pressed = action;
        _worker.SetPressedAction(action);

        if (action == PanelAction.Volume)
        {
            _draggingVolume = true;
            _worker.SetVolumeFraction((point.Value.X - theme.SliderLeft) / Math.Max(1, theme.SliderWidth));
            PreviewFrame.CaptureMouse();
        }

        if (action == PanelAction.Lock)
        {
            StartLockHold();
        }

        e.Handled = true;
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_draggingVolume || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var point = ToPanelPoint(e);
        if (point is null)
        {
            return;
        }

        var theme = PanelThemes.Get(_store.Config.ThemeId);
        _worker.SetVolumeFraction((point.Value.X - theme.SliderLeft) / Math.Max(1, theme.SliderWidth));
    }

    private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_pressed is null)
        {
            return;
        }

        var action = _pressed.Value;
        _pressed = null;
        _draggingVolume = false;
        PreviewFrame.ReleaseMouseCapture();
        CancelLockHold();

        if (action == PanelAction.Seek && PanelThemes.Get(_store.Config.ThemeId) is ISeekTheme seek && ToPanelPoint(e) is { } seekPoint)
            _worker.SetPlaybackFraction(Math.Clamp((seekPoint.X - seek.SeekLeft) / Math.Max(1, seek.SeekWidth), 0, 1));

        if (action is not (PanelAction.Volume or PanelAction.Lock or PanelAction.Seek))
        {
            _worker.PerformAction(action);
        }

        _worker.SetPressedAction(null);
        e.Handled = true;
    }

    private Point? ToPanelPoint(MouseEventArgs e)
    {
        if (PreviewImage.ActualWidth <= 0 || PreviewImage.ActualHeight <= 0)
        {
            return null;
        }

        var available = e.GetPosition(PreviewImage);
        var scale = Math.Min(PreviewImage.ActualWidth / DesignWidth, PreviewImage.ActualHeight / DesignHeight);
        if (scale <= 0)
        {
            return null;
        }

        var drawnWidth = DesignWidth * scale;
        var drawnHeight = DesignHeight * scale;
        var offsetX = (PreviewImage.ActualWidth - drawnWidth) / 2;
        var offsetY = (PreviewImage.ActualHeight - drawnHeight) / 2;
        var x = (available.X - offsetX) / scale;
        var y = (available.Y - offsetY) / scale;

        if (x < 0 || y < 0 || x > DesignWidth || y > DesignHeight)
        {
            return null;
        }

        return new Point(x, y);
    }

    private System.Windows.Threading.DispatcherTimer? _lockTimer;

    private void StartLockHold()
    {
        var started = DateTime.UtcNow;
        _lockTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(80),
        };
        _lockTimer.Tick += (_, _) =>
        {
            var progress = (DateTime.UtcNow - started).TotalSeconds / 1.2;
            _worker.SetLockProgress(progress);
            if (progress >= 1)
            {
                CancelLockHold();
                _worker.SetPressedAction(null);
                _worker.PerformAction(PanelAction.Lock);
            }
        };
        _lockTimer.Start();
    }

    private void CancelLockHold()
    {
        if (_lockTimer is not null)
        {
            _lockTimer.Stop();
            _lockTimer = null;
        }

        _worker.SetLockProgress(0);
    }
}
