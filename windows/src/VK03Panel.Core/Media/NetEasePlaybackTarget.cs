using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace VK03Panel.Core.Media;

/// <summary>Playlist-only integration; no account credentials or share-tracking parameters are stored.</summary>
public static class NetEasePlaybackTarget
{
    public static bool TryGetPlaylistId(string? input, out string id)
    {
        id = string.Empty;
        var text = input?.Trim() ?? string.Empty;
        if (long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0)
        { id = number.ToString(CultureInfo.InvariantCulture); return true; }
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || !uri.Host.Equals("music.163.com", StringComparison.OrdinalIgnoreCase)) return false;
        var query = uri.Query;
        if (uri.AbsolutePath == "/" && uri.Fragment.StartsWith("#/playlist?", StringComparison.OrdinalIgnoreCase))
            query = uri.Fragment[uri.Fragment.IndexOf('?')..];
        else if (!uri.AbsolutePath.TrimEnd('/').Equals("/playlist", StringComparison.OrdinalIgnoreCase)) return false;
        foreach (var part in query.TrimStart('?').Split('&'))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2 || pair[0] != "id") continue;
            if (long.TryParse(pair[1], NumberStyles.None, CultureInfo.InvariantCulture, out number) && number > 0)
            { id = number.ToString(CultureInfo.InvariantCulture); return true; }
        }
        return false;
    }

    public static string BuildPlayCommand(string playlistId)
    {
        if (!TryGetPlaylistId(playlistId, out var id)) throw new ArgumentException("歌单 ID 无效");
        // The installed 3.1 client consumes an orpheus Base64 JSON command via --webcmd.
        var payload = JsonSerializer.Serialize(new { cmd = "play", type = "playlist", id, channel = "vk03-panel" });
        return "orpheus://" + Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }
    public static string? FindExecutable()
    {
        const string key = @"Software\Microsoft\Windows\CurrentVersion\App Paths\cloudmusic.exe";
        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using var entry = root.OpenSubKey(key);
            var path = (entry?.GetValue(null) as string)?.Trim('"');
            if (path is not null && File.Exists(path) && Path.GetFileName(path).Equals("cloudmusic.exe", StringComparison.OrdinalIgnoreCase)) return path;
        }
        foreach (var folder in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
        {
            var path = Path.Combine(folder, "Netease", "CloudMusic", "cloudmusic.exe");
            if (File.Exists(path)) return path;
        }
        return null;
    }
    public static bool Launch(string playlistId)
    {
        var executable = FindExecutable() ?? throw new FileNotFoundException("未找到网易云音乐客户端");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable)! };
        start.ArgumentList.Add("--webcmd=" + BuildPlayCommand(playlistId));
        using var process = Process.Start(start);
        return process is not null;
    }
}

internal sealed class PlayButtonController(Func<bool> hasSession, Func<Task<bool>> toggle, Func<string, bool> launch, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private int _pending;
    private bool _launched;
    private long _launchTimestamp;
    public async Task<string> ExecuteAsync(bool fallbackEnabled, string playlistId)
    {
        if (Interlocked.CompareExchange(ref _pending, 1, 0) != 0) return "正在处理播放请求";
        try
        {
            // An existing session always wins, even when paused or unable to accept Play.
            if (hasSession()) return await toggle() ? "已请求暂停/继续播放" : "当前播放器未响应播放控制";
            if (!fallbackEnabled || !NetEasePlaybackTarget.TryGetPlaylistId(playlistId, out var id))
                return "当前没有媒体会话，可在“播放”页设置空闲歌单";
            if (_launched && _clock.GetElapsedTime(_launchTimestamp, _clock.GetTimestamp()) < TimeSpan.FromSeconds(15))
                return "网易云正在启动，请稍候";
            if (!launch(id)) return "未能启动网易云音乐";
            _launched = true; _launchTimestamp = _clock.GetTimestamp();
            return "已请求网易云播放歌单";
        }
        catch (Exception ex) { return "播放失败：" + ex.Message; }
        finally { Volatile.Write(ref _pending, 0); }
    }
}
