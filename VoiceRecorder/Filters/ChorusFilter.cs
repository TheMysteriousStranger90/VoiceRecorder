using System.Diagnostics.CodeAnalysis;
using CSCore;
using CSCore.Streams.Effects;

namespace VoiceRecorder.Filters;

/// <summary>
/// Chorus effect - creates a richer, fuller sound by layering slightly detuned copies
/// </summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Filter used across assemblies")]
public sealed class ChorusFilter : DmoFilterBase
{
    public override string Name => "Chorus";
    public override string Description => "Creates a richer sound by layering detuned copies";

    /// <summary>
    /// Wet/Dry mix (0-100)
    /// </summary>
    public float WetDryMix { get; set; } = 50f;

    /// <summary>
    /// Modulation depth (0-100)
    /// </summary>
    public float Depth { get; set; } = 25f;

    /// <summary>
    /// Feedback percentage (-99 to 99)
    /// </summary>
    public float Feedback { get; set; } = 15f;

    /// <summary>
    /// LFO frequency in Hz (0 to 10)
    /// </summary>
    public float Frequency { get; set; } = 0.8f;

    /// <summary>
    /// Delay in milliseconds (0 to 20)
    /// </summary>
    public float Delay { get; set; } = 12f;

    /// <summary>
    /// LFO waveform shape
    /// </summary>
    public ChorusWaveform Waveform { get; set; } = ChorusWaveform.WaveformSin;

    /// <summary>
    /// Phase offset between channels (0-4, representing 0-180 degrees)
    /// </summary>
    public int Phase { get; set; } = 2;

    protected override IWaveSource ApplyDmoEffect(IWaveSource source)
    {
        return new DmoChorusEffect(source)
        {
            WetDryMix = Math.Clamp(WetDryMix, 0f, 100f),
            Depth = Math.Clamp(Depth, 0f, 100f),
            Feedback = Math.Clamp(Feedback, -99f, 99f),
            Frequency = Math.Clamp(Frequency, 0f, 10f),
            Delay = Math.Clamp(Delay, 0f, 20f),
            Waveform = Waveform,
            Phase = (ChorusPhase)Math.Clamp(Phase, 0, 4)
        };
    }
}
