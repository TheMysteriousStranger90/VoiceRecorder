using System.Diagnostics.CodeAnalysis;
using VoiceRecorder.Models;

namespace VoiceRecorder.Interfaces;

[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Interfaces used across assemblies")]
public interface ISettingsService
{
    AudioSettings LoadSettings();
    void SaveSettings(AudioSettings settings);
}
