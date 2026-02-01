using System.Diagnostics.CodeAnalysis;
using CSCore;
using CSCore.Streams.Effects;

namespace VoiceRecorder.Filters;

/// <summary>
/// Echo effect - creates distinct repetitions of the sound
/// </summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Filter used across assemblies")]
public sealed class EchoFilter : DmoFilterBase
{
    public override string Name => "Echo";
    public override string Description => "Creates distinct sound repetitions with decay";

    /// <summary>
    /// Wet/Dry mix (0-100). Higher = more echo effect
    /// </summary>
    public float WetDryMix { get; set; } = 35f;

    /// <summary>
    /// Feedback percentage (0-100). Higher = more repetitions
    /// </summary>
    public float Feedback { get; set; } = 40f;

    /// <summary>
    /// Left channel delay in milliseconds (1-2000)
    /// </summary>
    public float LeftDelay { get; set; } = 250f;

    /// <summary>
    /// Right channel delay in milliseconds (1-2000)
    /// </summary>
    public float RightDelay { get; set; } = 280f;

    /// <summary>
    /// Whether left and right channels are swapped
    /// </summary>
    public bool PanDelay { get; set; } = true;

    protected override IWaveSource ApplyDmoEffect(IWaveSource source)
    {
        return new DmoEchoEffect(source)
        {
            WetDryMix = Math.Clamp(WetDryMix, 0f, 100f),
            Feedback = Math.Clamp(Feedback, 0f, 100f),
            LeftDelay = Math.Clamp(LeftDelay, 1f, 2000f),
            RightDelay = Math.Clamp(RightDelay, 1f, 2000f),
            PanDelay = PanDelay
        };
    }
}
