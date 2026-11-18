using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;

namespace HyperMusNotifi;

public sealed class NotifyIconController : IDisposable
{
    private readonly NotifyIcon _icon;

    public NotifyIconController()
    {
        _icon = new NotifyIcon
        {
            Text = "HyperMusNotifi - 正在监听媒体",
            Visible = true
        };
        _icon.Icon = TryLoadIcon();
        var menu = new ContextMenuStrip();
        menu.Items.Add("退出", null, (_, _) => System.Windows.Application.Current.Shutdown());
        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (_, _) => { /* reserved */ };
    }

    private static Icon TryLoadIcon()
    {
        try
        {
            string? exePath = Path.Combine(
                AppContext.BaseDirectory, "HyperMusNotifi.exe");
            if (File.Exists(exePath))
                return Icon.ExtractAssociatedIcon(exePath) ?? SystemIcons.Application;
        }
        catch { /* fall through */ }
        return SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
