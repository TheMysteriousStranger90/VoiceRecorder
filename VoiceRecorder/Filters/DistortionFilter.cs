using System.Diagnostics.CodeAnalysis;
using CSCore;
using CSCore.Streams.Effects;

namespace VoiceRecorder.Filters;

/// <summary>
/// Distortion effect - adds harmonic distortion to the signal
/// </summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Filter used across assemblies")]
public sealed class DistortionFilter : DmoFilterBase
{
    public override string Name => "Distortion";
    public override string Description => "Adds harmonic distortion and edge to the sound";

    /// <summary>
    /// Distortion gain in dB (-60 to 0)
    /// </summary>
    public float Gain { get; set; } = -18f;

    /// <summary>
    /// Edge of distortion (0-100). Higher = more aggressive
    /// </summary>
    public float Edge { get; set; } = 50f;

    /// <summary>
    /// Post-distortion EQ center frequency in Hz (100 to 8000)
    /// </summary>
    public float PostEQCenterFrequency { get; set; } = 2400f;

    /// <summary>
    /// Post-distortion EQ bandwidth in Hz (100 to 8000)
    /// </summary>
    public float PostEQBandwidth { get; set; } = 2400f;

    /// <summary>
    /// Pre-lowpass filter cutoff in Hz (100 to 8000)
    /// </summary>
    public float PreLowpassCutoff { get; set; } = 8000f;

    protected override IWaveSource ApplyDmoEffect(IWaveSource source)
    {
        return new DmoDistortionEffect(source)
        {
            Gain = Math.Clamp(Gain, -60f, 0f),
            Edge = Math.Clamp(Edge, 0f, 100f),
            PostEQCenterFrequency = Math.Clamp(PostEQCenterFrequency, 100f, 8000f),
            PostEQBandwidth = Math.Clamp(PostEQBandwidth, 100f, 8000f),
            PreLowpassCutoff = Math.Clamp(PreLowpassCutoff, 100f, 8000f)
        };
    }
}
