using System.Drawing;
using System.Windows.Forms;

namespace VK03Panel.App;

/// <summary>Tray icon with the Win32 menu used by Windows 11 system-tray apps.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _win11Item;
    private readonly ToolStripMenuItem _macItem;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly Icon _icon;

    public event Action? OpenSettingsRequested;

    public event Action? ExitRequested;

    public event Action<string>? ThemeSelected;

    public event Action<bool>? PausedChanged;

    public TrayIcon(string themeId, bool paused)
    {
        _icon = LoadIcon();
        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            Font = new System.Drawing.Font("Segoe UI", 9f),
        };

        var open = new ToolStripMenuItem("打开设置") { Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold) };
        open.Click += (_, _) => OpenSettingsRequested?.Invoke();
        menu.Items.Add(open);
        menu.Items.Add(new ToolStripSeparator());

        var themeMenu = new ToolStripMenuItem("副屏主题");
        _win11Item = new ToolStripMenuItem("Windows 11") { CheckOnClick = false };
        _macItem = new ToolStripMenuItem("macOS") { CheckOnClick = false };
        _win11Item.Click += (_, _) => ThemeSelected?.Invoke("win11");
        _macItem.Click += (_, _) => ThemeSelected?.Invoke("mac");
        themeMenu.DropDownItems.Add(_win11Item);
        themeMenu.DropDownItems.Add(_macItem);
        menu.Items.Add(themeMenu);

        _pauseItem = new ToolStripMenuItem("暂停刷新") { CheckOnClick = true };
        _pauseItem.CheckedChanged += (_, _) => PausedChanged?.Invoke(_pauseItem.Checked);
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new ToolStripSeparator());

        var exit = new ToolStripMenuItem("退出");
        exit.Click += (_, _) => ExitRequested?.Invoke();
        menu.Items.Add(exit);

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "VK03 副屏监控",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _notifyIcon.DoubleClick += (_, _) => OpenSettingsRequested?.Invoke();

        UpdateState(themeId, paused);
    }

    public void UpdateState(string themeId, bool paused)
    {
        _win11Item.Checked = string.Equals(themeId, "win11", StringComparison.OrdinalIgnoreCase);
        _macItem.Checked = string.Equals(themeId, "mac", StringComparison.OrdinalIgnoreCase);
        _pauseItem.Checked = paused;
    }

    public void ShowNotification(string title, string message)
    {
        try
        {
            _notifyIcon.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
        }
        catch (Exception)
        {
            // Notifications are optional.
        }
    }

    private static Icon LoadIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path))
            {
                var icon = Icon.ExtractAssociatedIcon(path);
                if (icon is not null)
                {
                    return icon;
                }
            }
        }
        catch (Exception)
        {
            // Fall through to the system icon.
        }

        return SystemIcons.Application;
    }

    public void Dispose()
    {
        try
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _icon.Dispose();
        }
        catch (Exception)
        {
            // Ignore tray teardown failures.
        }
    }
}
