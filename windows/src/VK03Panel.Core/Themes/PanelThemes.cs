namespace VK03Panel.Core.Themes;

/// <summary>Available panel skins.</summary>
public static class PanelThemes
{
    public const string MacId = "mac";
    public const string Win11Id = "win11";

    public static IPanelTheme Mac { get; } = new MacTheme();

    public static IPanelTheme Win11 { get; } = new Win11Theme();

    public static IReadOnlyList<IPanelTheme> All { get; } = new[] { Win11, Mac };

    public static IPanelTheme Get(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Win11;
        }

        foreach (var theme in All)
        {
            if (string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return theme;
            }
        }

        return Win11;
    }

    public static bool Exists(string? id) =>
        All.Any(theme => string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase));
}
