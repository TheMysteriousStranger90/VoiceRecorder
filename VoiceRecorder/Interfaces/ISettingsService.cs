using VoiceRecorder.Models;

namespace VoiceRecorder.Interfaces;

public interface ISettingsService
{
    AudioSettings LoadSettings();
    void SaveSettings(AudioSettings settings);
}
