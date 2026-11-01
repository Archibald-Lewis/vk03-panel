using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VK03Panel.Core.Config;

/// <summary>User settings persisted to %APPDATA%\VK03Panel\config.json.</summary>
public sealed class PanelConfig
{
    public string ThemeId { get; set; } = "win11";

    /// <summary>system | light | dark</summary>
    public string Appearance { get; set; } = "system";

    /// <summary>system | fixed</summary>
    public string Accent { get; set; } = "system";

    public string AccentHex { get; set; } = "#0078D4";

    /// <summary>0 | 90 | 180 | 270</summary>
    public int Rotation { get; set; } = 90;

    public bool StartWithWindows { get; set; }

    public bool StartMinimized { get; set; }

    public bool TurnOffPanelOnExit { get; set; } = true;

    public int RefreshPerSecond { get; set; } = 1;

    public bool IdlePlaybackEnabled { get; set; }

    public string IdlePlaylistId { get; set; } = string.Empty;

    public PanelConfig Clone() => new()
    {
        ThemeId = ThemeId,
        Appearance = Appearance,
        Accent = Accent,
        AccentHex = AccentHex,
        Rotation = Rotation,
        StartWithWindows = StartWithWindows,
        StartMinimized = StartMinimized,
        TurnOffPanelOnExit = TurnOffPanelOnExit,
        RefreshPerSecond = RefreshPerSecond,
        IdlePlaybackEnabled = IdlePlaybackEnabled,
        IdlePlaylistId = IdlePlaylistId,
    };

    public void Sanitize()
    {
        ThemeId = VK03Panel.Core.Themes.PanelThemes.Get(ThemeId).Id;

        Appearance = Appearance switch
        {
            "light" => "light",
            "dark" => "dark",
            _ => "system",
        };

        Accent = Accent == "fixed" ? "fixed" : "system";
        if (!System.Text.RegularExpressions.Regex.IsMatch(AccentHex ?? string.Empty, "^#[0-9a-fA-F]{6}$"))
        {
            AccentHex = "#0078D4";
        }

        if (Rotation is not (0 or 90 or 180 or 270))
        {
            Rotation = 90;
        }

        RefreshPerSecond = Math.Clamp(RefreshPerSecond, 1, 2);
        if (VK03Panel.Core.Media.NetEasePlaybackTarget.TryGetPlaylistId(IdlePlaylistId, out var playlistId)) IdlePlaylistId = playlistId;
        else { IdlePlaylistId = string.Empty; IdlePlaybackEnabled = false; }
    }
}

/// <summary>Loads and saves <see cref="PanelConfig"/> with corruption recovery.</summary>
public sealed class PanelConfigStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public PanelConfigStore()
    {
        DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VK03Panel");
        FilePath = Path.Combine(DirectoryPath, "config.json");
        Config = new PanelConfig();
    }

    public string DirectoryPath { get; }

    public string FilePath { get; }

    public PanelConfig Config { get; private set; }

    public void Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                Config = new PanelConfig();
                Config.Sanitize();
                Save();
                return;
            }

            var json = File.ReadAllText(FilePath);
            var loaded = JsonSerializer.Deserialize<PanelConfig>(json, Options);
            Config = loaded ?? new PanelConfig();
            Config.Sanitize();
        }
        catch (Exception)
        {
            TryBackupCorruptFile();
            Config = new PanelConfig();
            Config.Sanitize();
            Save();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            Config.Sanitize();
            var json = JsonSerializer.Serialize(Config, Options);
            var temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, FilePath, overwrite: true);
        }
        catch (Exception)
        {
            // Configuration persistence must never crash the panel.
        }
    }

    private void TryBackupCorruptFile()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                File.Copy(FilePath, FilePath + ".bak", overwrite: true);
            }
        }
        catch (Exception)
        {
            // Ignore backup failures.
        }
    }
}

/// <summary>Well-known application folders.</summary>
public static class AppPaths
{
    public static string ConfigDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VK03Panel");

    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VK03Panel");

    public static string LogDirectory => Path.Combine(DataDirectory, "logs");

    public static string ConfigFile => Path.Combine(ConfigDirectory, "config.json");

    public static string DiagnosticsFile(string name) => Path.Combine(DataDirectory, name);
}
