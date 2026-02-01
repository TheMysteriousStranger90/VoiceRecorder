using System.Diagnostics.CodeAnalysis;
using CSCore;
using CSCore.Streams.Effects;

namespace VoiceRecorder.Filters;

/// <summary>
/// Gargle effect - creates a modulated, tremolo-like sound
/// </summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Filter used across assemblies")]
public sealed class GargleFilter : DmoFilterBase
{
    public override string Name => "Gargle";
    public override string Description => "Creates a tremolo-like modulated sound effect";

    /// <summary>
    /// Rate of modulation in Hz (1 to 1000)
    /// </summary>
    public int RateHz { get; set; } = 20;

    /// <summary>
    /// Waveform shape for modulation
    /// </summary>
    public GargleWaveShape WaveShape { get; set; } = GargleWaveShape.Triangle;

    protected override IWaveSource ApplyDmoEffect(IWaveSource source)
    {
        return new DmoGargleEffect(source)
        {
            RateHz = Math.Clamp(RateHz, 1, 1000),
            WaveShape = WaveShape
        };
    }
}
