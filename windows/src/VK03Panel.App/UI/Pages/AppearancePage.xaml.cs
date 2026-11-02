using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VK03Panel.App;
using VK03Panel.Core.Cli;
using VK03Panel.Core.Config;
using VK03Panel.Core.Rendering;
using VK03Panel.Core.Runtime;
using VK03Panel.Core.Themes;

namespace VK03Panel.App.UI.Pages;

public partial class AppearancePage : UserControl, ISettingsPage, IThemeAware
{
    private static readonly string[] PresetColors =
    {
        "#0078D4",
        "#4CC2FF",
        "#8764B8",
        "#C239B3",
        "#E74856",
        "#00B294",
    };

    private readonly PanelConfigStore _store;
    private readonly ThemeManager _theme;
    private bool _initializing = true;

    public AppearancePage(PanelConfigStore store, ThemeManager theme)
    {
        _store = store;
        _theme = theme;
        InitializeComponent();

        ThemeWin11.IsChecked = string.Equals(store.Config.ThemeId, "win11", StringComparison.OrdinalIgnoreCase);
        ThemeMac.IsChecked = string.Equals(store.Config.ThemeId, "mac", StringComparison.OrdinalIgnoreCase);
        ThemeNebula.IsChecked = store.Config.ThemeId == PanelThemes.NebulaId;
        ThemeImmersive.IsChecked = store.Config.ThemeId == PanelThemes.ImmersiveId;
        ThemeSketch.IsChecked = store.Config.ThemeId == PanelThemes.SketchId;
        ThemeAnime.IsChecked = store.Config.ThemeId == PanelThemes.AnimeId;

        switch (store.Config.Appearance)
        {
            case "light":
                AppearanceLight.IsChecked = true;
                break;
            case "dark":
                AppearanceDark.IsChecked = true;
                break;
            default:
                AppearanceSystem.IsChecked = true;
                break;
        }

        AccentSystem.IsChecked = store.Config.Accent != "fixed";
        AccentFixed.IsChecked = store.Config.Accent == "fixed";

        BuildSwatches();
        RenderThumbnails();
        _initializing = false;
    }

    public void UpdateStatus(PanelWorkerStatus status)
    {
    }

    public void OnThemeChanged()
    {
        RenderThumbnails();
    }

    private void BuildSwatches()
    {
        SwatchPanel.Children.Clear();
        foreach (var hex in PresetColors)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var swatch = new RadioButton
            {
                GroupName = "AccentColor",
                Style = (Style)FindResource("ColorSwatch"),
                Background = new SolidColorBrush(color),
                Tag = hex,
                ToolTip = hex,
            };
            swatch.Checked += OnAccentColorChecked;
            SwatchPanel.Children.Add(swatch);

            if (string.Equals(_store.Config.AccentHex, hex, StringComparison.OrdinalIgnoreCase))
            {
                swatch.IsChecked = true;
            }
        }
    }

    private void RenderThumbnails()
    {
        try
        {
            var dark = _theme.IsDark;
            var model = DemoData.Create(dark);
            var win11 = PanelRenderer.Render(PanelThemes.Win11, model, 90);
            var mac = PanelRenderer.Render(PanelThemes.Mac, model, 90);
            if (win11.Bitmap.CanFreeze)
            {
                win11.Bitmap.Freeze();
            }

            if (mac.Bitmap.CanFreeze)
            {
                mac.Bitmap.Freeze();
            }

            ThumbWin11.Source = win11.Bitmap;
            ThumbMac.Source = mac.Bitmap;
            var nebula = PanelRenderer.Render(PanelThemes.Nebula, model, 90);
            var immersive = PanelRenderer.Render(PanelThemes.Immersive, model, 90);
            nebula.Bitmap.Freeze(); immersive.Bitmap.Freeze();
            ThumbNebula.Source = nebula.Bitmap;
            ThumbImmersive.Source = immersive.Bitmap;
            var sketch = PanelRenderer.Render(PanelThemes.Sketch, model, 90);
            var anime = PanelRenderer.Render(PanelThemes.Anime, model, 90);
            sketch.Bitmap.Freeze(); anime.Bitmap.Freeze();
            ThumbSketch.Source = sketch.Bitmap;
            ThumbAnime.Source = anime.Bitmap;
        }
        catch (Exception)
        {
            // Thumbnails are cosmetic.
        }
    }

    private void OnThemeChecked(object sender, RoutedEventArgs e)
    {
        if (_initializing || sender is not RadioButton { Tag: string themeId })
        {
            return;
        }

        ((App)Application.Current).ApplyConfig(config => config.ThemeId = themeId);
    }

    private void OnAppearanceChecked(object sender, RoutedEventArgs e)
    {
        if (_initializing || sender is not RadioButton { Tag: string appearance })
        {
            return;
        }

        ((App)Application.Current).ApplyConfig(config => config.Appearance = appearance);
        RenderThumbnails();
    }

    private void OnAccentModeChecked(object sender, RoutedEventArgs e)
    {
        if (_initializing || sender is not RadioButton { Tag: string mode })
        {
            return;
        }

        ((App)Application.Current).ApplyConfig(config => config.Accent = mode);
    }

    private void OnAccentColorChecked(object sender, RoutedEventArgs e)
    {
        if (_initializing || sender is not RadioButton { Tag: string hex })
        {
            return;
        }

        ((App)Application.Current).ApplyConfig(config =>
        {
            config.Accent = "fixed";
            config.AccentHex = hex;
        });

        AccentFixed.IsChecked = true;
    }
}
