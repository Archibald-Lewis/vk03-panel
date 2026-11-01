using System.Windows;
using System.Windows.Controls;
using VK03Panel.Core.Config;
using VK03Panel.Core.Media;
using VK03Panel.Core.Runtime;

namespace VK03Panel.App.UI.Pages;

public partial class PlaybackPage : UserControl, ISettingsPage
{
    private readonly PanelConfigStore _store;
    private bool _initializing = true;
    private readonly string _installation;
    public PlaybackPage(PanelWorker worker, PanelConfigStore store)
    {
        _store = store;
        InitializeComponent();
        PlaylistInput.Text = string.IsNullOrEmpty(store.Config.IdlePlaylistId) ? string.Empty : "https://music.163.com/playlist?id=" + store.Config.IdlePlaylistId;
        FallbackToggle.IsChecked = store.Config.IdlePlaybackEnabled;
        _installation = NetEasePlaybackTarget.FindExecutable() is null ? "未检测到网易云音乐客户端" : "已检测到网易云音乐客户端";
        _initializing = false;
        UpdateStatus(worker.Status);
    }
    public void UpdateStatus(PanelWorkerStatus status) => PlaybackStatusText.Text = status.PlaybackMessage ?? _installation;
    private void OnFallbackChanged(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        if (FallbackToggle.IsChecked == true && string.IsNullOrEmpty(_store.Config.IdlePlaylistId))
        {
            _initializing = true; FallbackToggle.IsChecked = false; _initializing = false;
            SaveMessage.Text = "请先保存一个网易云歌单链接。"; return;
        }
        ((App)Application.Current).ApplyConfig(c => c.IdlePlaybackEnabled = FallbackToggle.IsChecked == true);
    }
    private void OnSavePlaylist(object sender, RoutedEventArgs e)
    {
        if (!NetEasePlaybackTarget.TryGetPlaylistId(PlaylistInput.Text, out var id))
        { SaveMessage.Text = "请输入有效的网易云歌单链接或数字 ID。"; return; }
        ((App)Application.Current).ApplyConfig(c => { c.IdlePlaylistId = id; c.IdlePlaybackEnabled = true; });
        PlaylistInput.Text = "https://music.163.com/playlist?id=" + id;
        _initializing = true; FallbackToggle.IsChecked = true; _initializing = false;
        SaveMessage.Text = "歌单已保存在本机，空闲播放已启用。";
    }
}
