using System.Diagnostics.CodeAnalysis;
using VoiceRecorder.Models.Enums;

namespace VoiceRecorder.Models;

[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Model used across assemblies")]
public sealed class AudioSettings
{
    public int SampleRate { get; set; } = 44100;
    public int BitsPerSample { get; set; } = 16;
    public int Channels { get; set; } = 2;
    public ThemeVariant Theme { get; set; } = ThemeVariant.Main;
    public OutputFormat OutputFormat { get; set; } = OutputFormat.Wav;
    public int Mp3Bitrate { get; set; } = 192;
    public bool EnableNoiseReduction { get; set; }
    public bool AutoGainControl { get; set; }
    public float NoiseGateThreshold { get; set; } = -40f;

    public bool IsValid()
    {
        return SampleRate >= 1000 && SampleRate <= 200000 &&
               (BitsPerSample == 8 || BitsPerSample == 16 || BitsPerSample == 24 || BitsPerSample == 32) &&
               (Channels == 1 || Channels == 2) &&
               Mp3Bitrate >= 64 && Mp3Bitrate <= 320;
    }
}
