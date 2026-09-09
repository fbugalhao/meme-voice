// src/MemeVoice.App/TrayIconManager.cs
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace MemeVoice.App;

public sealed class TrayIconManager
{
    private readonly Window _window;
    private readonly NotifyIcon _notifyIcon;

    public TrayIconManager(Window window)
    {
        _window = window;
        _notifyIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Visible = false,
            Text = "MemeVoice"
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir", null, (_, _) => Restore());
        menu.Items.Add("Sair", null, (_, _) => {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Application.Current.Shutdown();
        });
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => Restore();
    }

    public void Attach()
    {
        _window.StateChanged += (_, _) =>
        {
            if (_window.WindowState == WindowState.Minimized)
            {
                _window.Hide();
                _notifyIcon.Visible = true;
            }
        };
    }

    private void Restore()
    {
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
        _notifyIcon.Visible = false;
    }
}
