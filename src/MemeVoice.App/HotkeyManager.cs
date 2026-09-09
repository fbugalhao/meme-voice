// src/MemeVoice.App/HotkeyManager.cs
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MemeVoice.App;

public sealed class HotkeyManager
{
    private const int WM_HOTKEY = 0x0312;
    private const int ToggleHotkeyId = 1;
    private const int NextEffectHotkeyId = 2;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_ALT = 0x0001;
    private const uint VK_V = 0x56;
    private const uint VK_N = 0x4E;

    private readonly Window _window;
    private HwndSource? _source;
    private Action? _toggleCallback;
    private Action? _nextEffectCallback;

    public event Action<string>? OnRegistrationFailed;

    public HotkeyManager(Window window)
    {
        _window = window;
    }

    public bool RegisterToggleHotkey(Action callback)
    {
        _toggleCallback = callback;
        return RegisterHotkeyInternal(ToggleHotkeyId, MOD_CONTROL | MOD_ALT, VK_V, "Ctrl+Alt+V");
    }

    public bool RegisterNextEffectHotkey(Action callback)
    {
        _nextEffectCallback = callback;
        return RegisterHotkeyInternal(NextEffectHotkeyId, MOD_CONTROL | MOD_ALT, VK_N, "Ctrl+Alt+N");
    }

    private bool RegisterHotkeyInternal(int id, uint modifiers, uint key, string description)
    {
        EnsureSourceAttached();
        var handle = new System.Windows.Interop.WindowInteropHelper(_window).Handle;

        bool success = RegisterHotKey(handle, id, modifiers, key);
        if (!success)
        {
            OnRegistrationFailed?.Invoke($"Não foi possível registrar o atalho {description} (talvez já esteja em uso por outro programa).");
        }
        return success;
    }

    private void EnsureSourceAttached()
    {
        if (_source != null) return;

        var handle = new System.Windows.Interop.WindowInteropHelper(_window).Handle;
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == ToggleHotkeyId) _toggleCallback?.Invoke();
            else if (id == NextEffectHotkeyId) _nextEffectCallback?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
