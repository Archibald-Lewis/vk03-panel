using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using VK03Panel.Core.Config;

namespace VK03Panel.App;

/// <summary>
/// Applies the Windows 11 look to the settings window: system/dark appearance, accent colour
/// resources for the Fluent control styles, rounded DWM corners and immersive dark title bar.
/// </summary>
public sealed class ThemeManager
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;

    public bool IsDark { get; private set; } = true;

    public Color Accent { get; private set; } = Color.FromRgb(0x4C, 0xC2, 0xFF);

    public Color AccentText { get; private set; } = Color.FromRgb(0x06, 0x14, 0x1E);

    public event Action? Changed;

    public void Apply(PanelConfig config)
    {
        IsDark = config.Appearance switch
        {
            "dark" => true,
            "light" => false,
            _ => !SystemUsesLightTheme(),
        };

        Accent = config.Accent == "fixed" ? ParseHex(config.AccentHex) : SystemAccent();
        AccentText = ContrastText(Accent);

        var resources = System.Windows.Application.Current.Resources;
        if (resources is null)
        {
            return;
        }

        resources["AppBackgroundBrush"] = Brush(IsDark ? 0x20 : 0xF3, IsDark ? 0x20 : 0xF3, IsDark ? 0x20 : 0xF3);
        resources["NavBackgroundBrush"] = Brush(IsDark ? 0x1D : 0xEC, IsDark ? 0x1D : 0xEC, IsDark ? 0x1D : 0xEC);
        resources["CardBackgroundBrush"] = Brush(IsDark ? 0x2B : 0xFF, IsDark ? 0x2B : 0xFF, IsDark ? 0x2B : 0xFF);
        resources["CardBorderBrush"] = Brush(
            IsDark ? 0x3A : 0xE2,
            IsDark ? 0x3A : 0xE2,
            IsDark ? 0x3A : 0xE2);
        resources["TextPrimaryBrush"] = Brush(
            IsDark ? 0xFF : 0x1A,
            IsDark ? 0xFF : 0x1A,
            IsDark ? 0xFF : 0x1A);
        resources["TextSecondaryBrush"] = Brush(
            IsDark ? 0xC6 : 0x5C,
            IsDark ? 0xC6 : 0x5C,
            IsDark ? 0xC6 : 0x5C);
        resources["TextTertiaryBrush"] = Brush(
            IsDark ? 0x9A : 0x8A,
            IsDark ? 0x9A : 0x8A,
            IsDark ? 0x9A : 0x8A);
        resources["SubtleFillBrush"] = Brush(
            IsDark ? 0x2E : 0xEB,
            IsDark ? 0x2E : 0xEB,
            IsDark ? 0x2E : 0xEB);
        resources["SubtleHoverBrush"] = Brush(
            IsDark ? 0x38 : 0xE0,
            IsDark ? 0x38 : 0xE0,
            IsDark ? 0x38 : 0xE0);
        resources["ControlBorderBrush"] = Brush(
            IsDark ? 0x4A : 0xC8,
            IsDark ? 0x4A : 0xC8,
            IsDark ? 0x4A : 0xC8);
        resources["DividerBrush"] = Brush(
            IsDark ? 0x33 : 0xE6,
            IsDark ? 0x33 : 0xE6,
            IsDark ? 0x33 : 0xE6);
        resources["TrackBrush"] = Brush(
            IsDark ? 0x55 : 0xC9,
            IsDark ? 0x55 : 0xC9,
            IsDark ? 0x55 : 0xC9);
        resources["ToggleThumbBrush"] = Brush(0xFF, 0xFF, 0xFF);
        resources["AccentBrush"] = new SolidColorBrush(Accent);
        resources["AccentTextBrush"] = new SolidColorBrush(AccentText);
        resources["AccentSubtleBrush"] = new SolidColorBrush(Color.FromArgb(46, Accent.R, Accent.G, Accent.B));
        resources["AccentHoverBrush"] = new SolidColorBrush(Lighten(Accent, IsDark ? 0.12 : -0.08));

        Changed?.Invoke();
    }

    public void ApplyWindow(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var dark = IsDark ? 1 : 0;
            DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
            var corner = 2; // DWMWCP_ROUND
            DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref corner, sizeof(int));
            var backdrop = IsDark ? 2 : 2; // DWMSBT_MAINWINDOW (Mica)
            DwmSetWindowAttribute(handle, DwmwaSystemBackdropType, ref backdrop, sizeof(int));
        }
        catch (Exception)
        {
            // Older Windows builds do not support every attribute.
        }
    }

    public static bool SystemUsesLightTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static Color SystemAccent()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("AccentColor") is int abgr)
            {
                var value = unchecked((uint)abgr);
                return Color.FromRgb((byte)(value & 0xFF), (byte)((value >> 8) & 0xFF), (byte)((value >> 16) & 0xFF));
            }
        }
        catch (Exception)
        {
            // Fall through to the Windows 11 default accent.
        }

        return Color.FromRgb(0x00, 0x78, 0xD4);
    }

    private static Color ParseHex(string? hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex ?? "#0078D4");
        }
        catch (Exception)
        {
            return Color.FromRgb(0x00, 0x78, 0xD4);
        }
    }

    private static SolidColorBrush Brush(int r, int g, int b)
    {
        var brush = new SolidColorBrush(Color.FromRgb((byte)r, (byte)g, (byte)b));
        brush.Freeze();
        return brush;
    }

    private static Color ContrastText(Color accent)
    {
        var luminance = ((0.299 * accent.R) + (0.587 * accent.G) + (0.114 * accent.B)) / 255.0;
        return luminance > 0.6 ? Color.FromRgb(0x06, 0x14, 0x1E) : Colors.White;
    }

    private static Color Lighten(Color color, double amount)
    {
        if (amount >= 0)
        {
            return Color.FromRgb(
                (byte)(color.R + ((255 - color.R) * amount)),
                (byte)(color.G + ((255 - color.G) * amount)),
                (byte)(color.B + ((255 - color.B) * amount)));
        }

        var factor = 1 + amount;
        return Color.FromRgb((byte)(color.R * factor), (byte)(color.G * factor), (byte)(color.B * factor));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
