using System.IO;
using System.Text.Json;

namespace MemeVoice.Core;

public sealed record AppConfig(
    string? InputDeviceId,
    string? OutputDeviceId,
    string LastEffectName,
    string ToggleHotkey,
    string NextEffectHotkey)
{
    public static AppConfig Default => new(
        InputDeviceId: null,
        OutputDeviceId: null,
        LastEffectName: "Grave/Robusto",
        ToggleHotkey: "Ctrl+Alt+V",
        NextEffectHotkey: "Ctrl+Alt+N");
}

public sealed class ConfigStore
{
    private readonly string _filePath;

    public ConfigStore(string filePath)
    {
        _filePath = filePath;
    }

    public AppConfig Load()
    {
        if (!File.Exists(_filePath)) return AppConfig.Default;

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? AppConfig.Default;
        }
        catch (JsonException)
        {
            return AppConfig.Default;
        }
        catch (IOException)
        {
            return AppConfig.Default;
        }
    }

    public void Save(AppConfig config)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}
