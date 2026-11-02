using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VK03Panel.App.UI.Pages;
using VK03Panel.Core.Config;
using VK03Panel.Core.Runtime;

namespace VK03Panel.App;

public sealed record NavItem(string Key, string Title, string Glyph);

public partial class MainWindow : Window
{
    private readonly PanelWorker _worker;
    private readonly PanelConfigStore _store;
    private readonly ThemeManager _theme;
    private readonly Dictionary<string, ISettingsPage> _pages = new();
    private bool _allowClose;

    public MainWindow(PanelWorker worker, PanelConfigStore store, ThemeManager theme)
    {
        _worker = worker;
        _store = store;
        _theme = theme;

        InitializeComponent();

        var items = new List<NavItem>
        {
            new("display", "副屏", "\uE7F4"),
            new("appearance", "外观", "\uE790"),
            new("playback", "播放", "\uE768"),
            new("data", "数据", "\uE9D9"),
            new("startup", "启动", "\uE7E8"),
            new("about", "关于", "\uE946"),
        };
        NavList.ItemsSource = items;
        NavList.SelectedIndex = 0;

        _worker.StatusChanged += OnWorkerStatusChanged;
        _worker.PreviewUpdated += OnPreviewUpdated;

        SourceInitialized += (_, _) => _theme.ApplyWindow(this);
        Closing += OnClosing;
        StateChanged += (_, _) => UpdateMaximizeGlyph();
    }

    public void ApplyTheme()
    {
        Background = (Brush)FindResource("AppBackgroundBrush");
        _theme.ApplyWindow(this);
        foreach (var page in _pages.Values.OfType<IThemeAware>())
        {
            page.OnThemeChanged();
        }
    }

    public void CloseForExit()
    {
        _allowClose = true;
        Close();
    }

    private void OnWorkerStatusChanged(PanelWorkerStatus status)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var accent = (Brush)FindResource("AccentBrush");
            var tertiary = (Brush)FindResource("TextTertiaryBrush");
            StatusDot.Fill = status.PanelConnected ? accent : new SolidColorBrush(Color.FromRgb(0xFF, 0xB9, 0x00));
            StatusText.Text = status.PanelConnected
                ? $"副屏在线 · {status.DataSource}"
                : status.TransportMessage;
            StatusDetail.Text = status.LastError ?? (status.PanelConnected ? string.Empty : "自动重连中");
            StatusDetail.Visibility = string.IsNullOrEmpty(StatusDetail.Text) ? Visibility.Collapsed : Visibility.Visible;

            if (!status.PanelConnected && status.LastError is null)
            {
                StatusDot.Fill = tertiary;
            }

            foreach (var page in _pages.Values)
            {
                page.UpdateStatus(status);
            }
        });
    }

    private void OnPreviewUpdated(System.Windows.Media.Imaging.BitmapSource preview)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_pages.TryGetValue("display", out var page) && page is DisplayPage display)
            {
                display.UpdatePreview(preview);
            }
        });
    }

    private void OnNavSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is not NavItem item)
        {
            return;
        }

        PageHost.Content = GetOrCreatePage(item.Key);
        ApplyStatusToPage();
    }

    private ISettingsPage GetOrCreatePage(string key)
    {
        if (_pages.TryGetValue(key, out var existing))
        {
            return existing;
        }

        ISettingsPage page = key switch
        {
            "appearance" => new AppearancePage(_store, _theme),
            "data" => new DataPage(_worker),
            "playback" => new PlaybackPage(_worker, _store),
            "startup" => new StartupPage(_store),
            "about" => new AboutPage(_worker),
            _ => new DisplayPage(_worker, _store, _theme),
        };

        _pages[key] = page;
        return page;
    }

    private void ApplyStatusToPage()
    {
        if (PageHost.Content is ISettingsPage page)
        {
            page.UpdateStatus(_worker.Status);
            if (page is DisplayPage display)
            {
                display.UpdatePreview(null);
            }
        }
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnMinimizeClicked(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClicked(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Hide();

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        UpdateMaximizeGlyph();
    }

    private void UpdateMaximizeGlyph()
    {
        if (MaximizeGlyph is null)
        {
            return;
        }

        MaximizeGlyph.Data = Geometry.Parse(
            WindowState == WindowState.Maximized
                ? "M0,2 H8 V10 H0 Z M2,0 H10 V8"
                : "M0,0 H10 V10 H0 Z");
    }

    /// <summary>Selects a settings page by key (used by --page for screenshots and support).</summary>
    public void SelectPage(string key)
    {
        for (var index = 0; index < NavList.Items.Count; index++)
        {
            if (NavList.Items[index] is NavItem item &&
                string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                NavList.SelectedIndex = index;
                return;
            }
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }
}
