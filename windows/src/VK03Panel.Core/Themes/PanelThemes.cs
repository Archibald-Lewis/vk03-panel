namespace VK03Panel.Core.Themes;

/// <summary>Available panel skins.</summary>
public static class PanelThemes
{
    public const string MacId = "mac";
    public const string NebulaId = "nebula";
    public const string ImmersiveId = "immersive";
    public const string Win11Id = "win11";

    public static IPanelTheme Mac { get; } = new MacTheme();

    public static IPanelTheme Win11 { get; } = new Win11Theme();

    public static IPanelTheme Nebula { get; } = new NebulaTheme();
    public static IPanelTheme Immersive { get; } = new ImmersiveTheme();
    public static IReadOnlyList<IPanelTheme> All { get; } = new[] { Win11, Mac, Nebula, Immersive };

    public static string NextId(string? id)
    {
        var index = All.ToList().FindIndex(theme => string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase));
        return All[(index + 1) % All.Count].Id;
    }

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
