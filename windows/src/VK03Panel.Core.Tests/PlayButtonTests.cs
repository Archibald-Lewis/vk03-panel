using System.Text;
using System.Text.Json;
using VK03Panel.Core.Media;
using VK03Panel.Core.Config;
using Xunit;

namespace VK03Panel.Core.Tests;

public sealed class PlayButtonTests
{
    [Theory]
    [InlineData("123456789", "123456789")]
    [InlineData("https://music.163.com/playlist?id=123456789&uct2=tracking-only", "123456789")]
    [InlineData("https://music.163.com/#/playlist?id=123456789", "123456789")]
    public void PlaylistTargetRetainsOnlyNumericId(string input, string expected)
    {
        Assert.True(NetEasePlaybackTarget.TryGetPlaylistId(input, out var id));
        Assert.Equal(expected, id);
        var config = new PanelConfig { IdlePlaylistId = input, IdlePlaybackEnabled = true };
        config.Sanitize(); Assert.Equal(expected, config.IdlePlaylistId);
        Assert.Equal(expected, config.Clone().IdlePlaylistId);
        Assert.True(config.Clone().IdlePlaybackEnabled);
        var command = NetEasePlaybackTarget.BuildPlayCommand(id);
        using var payload = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(command["orpheus://".Length..])));
        Assert.Equal("play", payload.RootElement.GetProperty("cmd").GetString());
        Assert.Equal("playlist", payload.RootElement.GetProperty("type").GetString());
        Assert.Equal(expected, payload.RootElement.GetProperty("id").GetString());
        Assert.DoesNotContain("uct2", command, StringComparison.OrdinalIgnoreCase);
    }
    [Theory]
    [InlineData("https://music.163.com.evil.example/playlist?id=123")]
    [InlineData("https://example.com/playlist?id=123")]
    [InlineData("https://music.163.com/song?id=123")]
    [InlineData("file:///C:/Windows/cmd.exe")]
    [InlineData("123&cmd=download")]
    public void InvalidTargetsDoNotBecomeLaunchCommands(string input)
    {
        Assert.False(NetEasePlaybackTarget.TryGetPlaylistId(input, out _));
        Assert.Throws<ArgumentException>(() => NetEasePlaybackTarget.BuildPlayCommand(input));
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExistingSessionAlwaysWinsOverConfiguredPlaylist(bool acceptsCommand)
    {
        var toggles = 0; var launches = 0;
        var player = new PlayButtonController(() => true, () => { toggles++; return Task.FromResult(acceptsCommand); }, _ => { launches++; return true; });
        await player.ExecuteAsync(true, "123456789");
        Assert.Equal(1, toggles); Assert.Equal(0, launches);
    }
    [Fact]
    public async Task IdleLaunchIsDebouncedWhileClientStarts()
    {
        var clock = new ManualClock(); var hasSession = false; var launches = 0; var toggles = 0;
        var player = new PlayButtonController(() => hasSession, () => { toggles++; return Task.FromResult(true); }, id => { Assert.Equal("123456789", id); launches++; return true; }, clock);
        for (var i = 0; i < 20; i++) { await player.ExecuteAsync(true, "123456789"); clock.Advance(TimeSpan.FromMilliseconds(100)); }
        Assert.Equal(1, launches); Assert.Equal(0, toggles);
        hasSession = true;
        await player.ExecuteAsync(true, "123456789");
        Assert.Equal(1, launches); Assert.Equal(1, toggles);
    }
    [Fact]
    public async Task DisabledFallbackDoesNotLaunchPlayer()
    {
        var launches = 0;
        var player = new PlayButtonController(() => false, () => Task.FromResult(false), _ => { launches++; return true; });
        await player.ExecuteAsync(false, "123456789");
        Assert.Equal(0, launches);
    }
    [Fact]
    public async Task InFlightToggleIsNotRepeatedByDoubleTap()
    {
        var completion = new TaskCompletionSource<bool>(); var toggles = 0;
        var player = new PlayButtonController(() => true, () => { toggles++; return completion.Task; }, _ => throw new InvalidOperationException());
        var first = player.ExecuteAsync(true, "123456789");
        await player.ExecuteAsync(true, "123456789");
        Assert.Equal(1, toggles);
        completion.SetResult(true); await first;
    }
    private sealed class ManualClock : TimeProvider
    {
        private long _now;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _now;
        public void Advance(TimeSpan value) => _now += value.Ticks;
    }
}
