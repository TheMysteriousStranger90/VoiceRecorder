using System.Diagnostics.CodeAnalysis;
using CSCore;
using CSCore.Streams.Effects;

namespace VoiceRecorder.Filters;

/// <summary>
/// Flanger effect - creates a sweeping, jet-like sound
/// </summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Filter used across assemblies")]
public sealed class FlangerFilter : DmoFilterBase
{
    public override string Name => "Flanger";
    public override string Description => "Creates a sweeping, jet-like modulation effect";

    /// <summary>
    /// Wet/Dry mix (0-100)
    /// </summary>
    public float WetDryMix { get; set; } = 45f;

    /// <summary>
    /// Modulation depth (0-100)
    /// </summary>
    public float Depth { get; set; } = 60f;

    /// <summary>
    /// Feedback percentage (-99 to 99)
    /// </summary>
    public float Feedback { get; set; } = 25f;

    /// <summary>
    /// LFO frequency in Hz (0 to 10)
    /// </summary>
    public float Frequency { get; set; } = 0.3f;

    /// <summary>
    /// Delay in milliseconds (0 to 4)
    /// </summary>
    public float Delay { get; set; } = 2f;

    /// <summary>
    /// LFO waveform shape
    /// </summary>
    public FlangerWaveform Waveform { get; set; } = FlangerWaveform.Sin;

    /// <summary>
    /// Phase offset (0-4, representing 0-180 degrees)
    /// </summary>
    public int Phase { get; set; } = 2;

    protected override IWaveSource ApplyDmoEffect(IWaveSource source)
    {
        return new DmoFlangerEffect(source)
        {
            WetDryMix = Math.Clamp(WetDryMix, 0f, 100f),
            Depth = Math.Clamp(Depth, 0f, 100f),
            Feedback = Math.Clamp(Feedback, -99f, 99f),
            Frequency = Math.Clamp(Frequency, 0f, 10f),
            Delay = Math.Clamp(Delay, 0f, 4f),
            Waveform = Waveform,
            Phase = (FlangerPhase)Math.Clamp(Phase, 0, 4)
        };
    }
}
