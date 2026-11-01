using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using VK03Panel.Core.Config;
using VK03Panel.Core.Runtime;
using VK03Panel.Core.Themes;

namespace VK03Panel.App.UI.Pages;

public partial class AboutPage : UserControl, ISettingsPage
{
    private readonly PanelWorker _worker;

    public AboutPage(PanelWorker worker)
    {
        _worker = worker;
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is null ? "1.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        DataPathText.Text = AppPaths.DataDirectory;
        UpdateStatus(worker.Status);
    }

    public void UpdateStatus(PanelWorkerStatus status)
    {
        DeviceText.Text = status.PanelConnected && !string.IsNullOrEmpty(status.DeviceSummary)
            ? status.DeviceSummary
            : status.TransportMessage;
        ThemeText.Text = PanelThemes.Get(status.ThemeId).DisplayName;
        SourceText.Text = status.DataSource;
    }

    private void OnOpenLink(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string url })
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Opening the browser is best effort.
        }
    }

    private void OnOpenConfigFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            System.IO.Directory.CreateDirectory(AppPaths.ConfigDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", AppPaths.ConfigDirectory) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Opening Explorer is best effort.
        }
    }
}
