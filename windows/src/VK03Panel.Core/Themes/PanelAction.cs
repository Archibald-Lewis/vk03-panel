namespace VK03Panel.Core.Themes;

/// <summary>User actions available on the panel, shared by both themes and the touch layer.</summary>
public enum PanelAction
{
    None = 0,
    Mute,
    Quieter,
    Volume,
    Louder,
    Previous,
    Play,
    Next,
    Lock,
    Seek,
}

public static class PanelActionExtensions
{
    public static bool IsTransport(this PanelAction action) =>
        action is PanelAction.Previous or PanelAction.Play or PanelAction.Next;

    public static string ToDisplayName(this PanelAction action) => action switch
    {
        PanelAction.Mute => "静音",
        PanelAction.Quieter => "音量－",
        PanelAction.Volume => "音量",
        PanelAction.Louder => "音量＋",
        PanelAction.Previous => "上一首",
        PanelAction.Play => "播放/暂停",
        PanelAction.Next => "下一首",
        PanelAction.Lock => "锁屏",
        PanelAction.Seek => "播放进度",
        _ => "无",
    };
}
