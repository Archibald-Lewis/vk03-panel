using System.Windows;
using System.Windows.Controls;
using VK03Panel.App;
using VK03Panel.Core.Config;
using VK03Panel.Core.Runtime;

namespace VK03Panel.App.UI.Pages;

public partial class StartupPage : UserControl, ISettingsPage
{
    private readonly PanelConfigStore _store;
    private bool _initializing = true;

    public StartupPage(PanelConfigStore store)
    {
        _store = store;
        InitializeComponent();

        AutoStartToggle.IsChecked = store.Config.StartWithWindows;
        MinimizedToggle.IsChecked = store.Config.StartMinimized;
        TurnOffToggle.IsChecked = store.Config.TurnOffPanelOnExit;
        _initializing = false;
    }

    public void UpdateStatus(PanelWorkerStatus status)
    {
    }

    private void OnToggleChanged(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        var autoStart = AutoStartToggle.IsChecked == true;
        var minimized = MinimizedToggle.IsChecked == true;
        var turnOff = TurnOffToggle.IsChecked == true;

        ((App)Application.Current).ApplyConfig(config =>
        {
            config.StartWithWindows = autoStart;
            config.StartMinimized = minimized;
            config.TurnOffPanelOnExit = turnOff;
        });
    }
}
