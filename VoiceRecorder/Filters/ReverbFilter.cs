using System.Diagnostics.CodeAnalysis;
using CSCore;
using CSCore.Streams.Effects;

namespace VoiceRecorder.Filters;

/// <summary>
/// Reverb effect - simulates acoustic space reflections
/// </summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Filter used across assemblies")]
public sealed class ReverbFilter : DmoFilterBase
{
    public override string Name => "Reverb";
    public override string Description => "Simulates room acoustics and spatial reflections";

    /// <summary>
    /// Input gain in dB (-96 to 0)
    /// </summary>
    public float InGain { get; set; } = -3f;

    /// <summary>
    /// Reverb mix in dB (-96 to 0). Higher = more reverb
    /// </summary>
    public float ReverbMix { get; set; } = -6f;

    /// <summary>
    /// Reverb time in milliseconds (0.001 to 3000)
    /// </summary>
    public float ReverbTime { get; set; } = 1200f;

    /// <summary>
    /// High frequency RT ratio (0.001 to 0.999). Lower = darker reverb
    /// </summary>
    public float HighFreqRTRatio { get; set; } = 0.6f;

    protected override IWaveSource ApplyDmoEffect(IWaveSource source)
    {
        return new DmoWavesReverbEffect(source)
        {
            InGain = Math.Clamp(InGain, -96f, 0f),
            ReverbMix = Math.Clamp(ReverbMix, -96f, 0f),
            ReverbTime = Math.Clamp(ReverbTime, 0.001f, 3000f),
            HighFrequencyRTRatio = Math.Clamp(HighFreqRTRatio, 0.001f, 0.999f)
        };
    }
}
