using System.Text.Json;
using VoiceRecorder.Interfaces;
using VoiceRecorder.Models;

namespace VoiceRecorder.Services;

internal sealed class SettingsService : ISettingsService
{
    private readonly string _settingsPath;

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true
    };

    public SettingsService()
    {
        string appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AzioVoiceRecorder");

        if (!Directory.Exists(appDataFolder))
        {
            Directory.CreateDirectory(appDataFolder);
        }

        _settingsPath = Path.Combine(appDataFolder, "settings.json");
    }

    public AudioSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                string json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<AudioSettings>(json);
                if (settings != null && settings.IsValid())
                {
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
        }

        return new AudioSettings();
    }

    public void SaveSettings(AudioSettings settings)
    {
        try
        {
            if (settings.IsValid())
            {
                string json = JsonSerializer.Serialize(settings, _serializerOptions);
                File.WriteAllText(_settingsPath, json);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
        }
    }
}
