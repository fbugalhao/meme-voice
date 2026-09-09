// src/MemeVoice.App/MainWindow.xaml.cs
using System;
using System.Linq;
using System.Windows;
using MemeVoice.Core;
using MemeVoice.Core.Effects;

namespace MemeVoice.App;

public partial class MainWindow : Window
{
    private readonly DeviceManager _deviceManager = new();
    private readonly ConfigStore _configStore;
    private readonly EffectChain _chain;
    private readonly AudioEngine _engine;
    private HotkeyManager? _hotkeyManager;
    private AppConfig _config;

    public MainWindow()
    {
        InitializeComponent();

        var appDataConfigPath = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "MemeVoice", "config.json");
        _configStore = new ConfigStore(appDataConfigPath);
        _config = _configStore.Load();

        var presets = EffectPresets.GetAll();
        EffectList.ItemsSource = presets;
        var initial = presets.FirstOrDefault(p => p.Name == _config.LastEffectName) ?? presets[0];
        EffectList.SelectedItem = initial;

        _chain = new EffectChain(initial.Effect);
        _engine = new AudioEngine(_chain);
        _engine.OnError += message => Dispatcher.Invoke(() => StatusText.Text = $"Erro: {message}");

        InputDeviceCombo.ItemsSource = _deviceManager.GetInputDevices();
        OutputDeviceCombo.ItemsSource = _deviceManager.GetOutputDevices();
        SelectConfiguredOrFirst(InputDeviceCombo, _config.InputDeviceId);
        SelectConfiguredOrFirst(OutputDeviceCombo, _config.OutputDeviceId);

        RefreshVbCableState();

        EffectList.SelectionChanged += (_, _) => OnEffectSelectionChanged();
        PitchLivreSlider.ValueChanged += (_, _) => OnPitchLivreChanged();

        new TrayIconManager(this).Attach();

        // Re-check VB-Cable presence whenever the window regains focus (e.g. after the user
        // installs VB-Cable and switches back, or restores the window from the tray), so the
        // warning and Start/Stop button state don't go stale for the life of the process.
        Activated += (_, _) => RefreshVbCableState();
    }

    // Refreshes the VB-Cable warning banner and the Start/Stop button's enabled state based on
    // current VB-Cable presence. VB-Cable presence gates the ability to *start* processing, but
    // must never disable the button while the engine is actively running, or the user would be
    // trapped with no UI way to stop already-routing audio if VB-Cable becomes unavailable
    // (e.g. uninstalled) while the app is running.
    private void RefreshVbCableState()
    {
        bool installed = _deviceManager.IsVbCableInstalled();
        VbCableWarning.Visibility = installed ? Visibility.Collapsed : Visibility.Visible;
        StartStopButton.IsEnabled = _engine.IsRunning || installed;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // The window's real Win32 HWND only exists once WPF has initialized the
        // HwndSource (guaranteed by this point), not during the constructor.
        // Registering hotkeys earlier would target IntPtr.Zero and silently fail.
        _hotkeyManager = new HotkeyManager(this);
        _hotkeyManager.OnRegistrationFailed += message => Dispatcher.Invoke(() => StatusText.Text = message);
        _hotkeyManager.RegisterToggleHotkey(() => Dispatcher.Invoke(ToggleProcessing));
        _hotkeyManager.RegisterNextEffectHotkey(() => Dispatcher.Invoke(SelectNextEffect));
    }

    private static void SelectConfiguredOrFirst(System.Windows.Controls.ComboBox combo, string? configuredId)
    {
        var items = combo.ItemsSource?.Cast<AudioDeviceInfo>().ToList();
        if (items == null || items.Count == 0) return;

        combo.SelectedItem = items.FirstOrDefault(d => d.Id == configuredId) ?? items[0];
    }

    private void OnEffectSelectionChanged()
    {
        if (EffectList.SelectedItem is not EffectPreset preset) return;

        bool isPitchLivre = preset.Name == "Pitch Livre";
        PitchLivreLabel.Visibility = isPitchLivre ? Visibility.Visible : Visibility.Collapsed;
        PitchLivreSlider.Visibility = isPitchLivre ? Visibility.Visible : Visibility.Collapsed;

        _chain.SetEffect(preset.Effect);
    }

    private void OnPitchLivreChanged()
    {
        if (_chain.Current is PitchShiftEffect pitchEffect)
        {
            pitchEffect.Semitones = (float)PitchLivreSlider.Value;
        }
    }

    public void ActivateEffectByName(string name)
    {
        if (EffectList.ItemsSource.Cast<EffectPreset>().FirstOrDefault(p => p.Name == name) is { } preset)
        {
            EffectList.SelectedItem = preset;
        }
    }

    private void SelectNextEffect()
    {
        var presets = EffectList.ItemsSource.Cast<Core.Effects.EffectPreset>().ToList();
        int currentIndex = EffectList.SelectedIndex;
        int nextIndex = (currentIndex + 1) % presets.Count;
        EffectList.SelectedItem = presets[nextIndex];
    }

    public void ToggleProcessing()
    {
        if (!StartStopButton.IsEnabled)
        {
            StatusText.Text = "VB-Cable não instalado. Instale o VB-Cable para iniciar o processamento.";
            return;
        }

        StartStopButton_Click(this, new RoutedEventArgs());
    }

    private void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_engine.IsRunning)
        {
            _engine.Stop();
            StartStopButton.Content = "Iniciar";
            StatusText.Text = "Parado";
        }
        else
        {
            var input = InputDeviceCombo.SelectedItem as AudioDeviceInfo;
            var output = OutputDeviceCombo.SelectedItem as AudioDeviceInfo;

            if (input == null || output == null)
            {
                StatusText.Text = "Selecione um dispositivo de entrada e saída.";
                return;
            }

            _engine.Start(input.Id, output.Id);
            StartStopButton.Content = _engine.IsRunning ? "Parar" : "Iniciar";
            StatusText.Text = _engine.IsRunning ? "Ativo" : "Erro ao iniciar";
        }

        SaveConfig();
    }

    private void SaveConfig()
    {
        var input = InputDeviceCombo.SelectedItem as AudioDeviceInfo;
        var output = OutputDeviceCombo.SelectedItem as AudioDeviceInfo;
        var effectName = (EffectList.SelectedItem as EffectPreset)?.Name ?? _config.LastEffectName;

        _config = _config with
        {
            InputDeviceId = input?.Id,
            OutputDeviceId = output?.Id,
            LastEffectName = effectName
        };
        _configStore.Save(_config);
    }
}
