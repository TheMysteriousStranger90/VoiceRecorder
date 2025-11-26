using VoiceRecorder.Interfaces;

namespace VoiceRecorder.Models;

public sealed class AudioSettings
{
    public int SampleRate { get; set; } = 44100;
    public int BitsPerSample { get; set; } = 16;
    public int Channels { get; set; } = 2;
    public ThemeVariant Theme { get; set; } = ThemeVariant.Main;

    public bool IsValid()
    {
        return SampleRate >= 1000 && SampleRate <= 200000 &&
               (BitsPerSample == 8 || BitsPerSample == 16 || BitsPerSample == 24 || BitsPerSample == 32) &&
               (Channels == 1 || Channels == 2);
    }
}
