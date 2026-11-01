using System.Windows;
using System.Windows.Media;

namespace VK03Panel.Core.Themes;

/// <summary>
/// A selectable panel skin. Themes own both painting and hit testing so the Windows 11
/// layout can differ from the macOS one while sharing the same actions.
/// </summary>
public interface IPanelTheme
{
    /// <summary>Stable id used in configuration ("mac" or "win11").</summary>
    string Id { get; }

    string DisplayName { get; }

    string Description { get; }

    Size DesignSize { get; }

    /// <summary>Rounded-rectangle bounds of an interactive element.</summary>
    Rect Bounds(PanelAction action);

    /// <summary>Returns the action whose bounds contain the logical panel point.</summary>
    PanelAction? HitTest(Point point);

    double SliderLeft { get; }

    double SliderWidth { get; }

    void Draw(PanelViewModel model, DrawingContext context, Size size);
}

/// <summary>Optional seek geometry for a playback-focused theme.</summary>
public interface ISeekTheme
{
    double SeekLeft { get; }
    double SeekWidth { get; }
}
