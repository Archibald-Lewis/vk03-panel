using VK03Panel.Core.Runtime;

namespace VK03Panel.App.UI.Pages;

/// <summary>Settings page contract: receives worker status once per rendered frame.</summary>
public interface ISettingsPage
{
    void UpdateStatus(PanelWorkerStatus status);
}

/// <summary>Page that repaints cached visuals when the Windows theme changes.</summary>
public interface IThemeAware
{
    void OnThemeChanged();
}
