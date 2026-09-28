// src/MemeVoice.App/TrayIconManager.cs
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using System.Drawing;
using System.IO;
using System.Reflection;

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
            Icon = LoadCustomIcon(),
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

    private Icon LoadCustomIcon()
    {
        try
        {
            // First try to load icon from embedded resource
            var assembly = typeof(TrayIconManager).Assembly;
            var resourceName = "MemeVoice.App.icon.ico";

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    return new Icon(stream);
                }
            }

            // If that fails, try to load from file path
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
            if (File.Exists(iconPath))
            {
                return new Icon(iconPath);
            }
        }
        catch (Exception ex)
        {
            // Log the error for debugging but fallback to system icon
            System.Diagnostics.Debug.WriteLine($"Failed to load custom icon: {ex.Message}");
        }

        return System.Drawing.SystemIcons.Application;
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
