using System.Diagnostics.CodeAnalysis;
using CSCore;
using CSCore.Streams.Effects;

namespace VoiceRecorder.Filters;

/// <summary>
/// Compressor effect - reduces dynamic range for more consistent volume
/// </summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Filter used across assemblies")]
public sealed class CompressorFilter : DmoFilterBase
{
    public override string Name => "Compressor";
    public override string Description => "Reduces dynamic range for consistent volume levels";

    /// <summary>
    /// Output gain in dB (-60 to 60)
    /// </summary>
    public float Gain { get; set; } = 4f;

    /// <summary>
    /// Attack time in milliseconds (0.01 to 500)
    /// </summary>
    public float Attack { get; set; } = 10f;

    /// <summary>
    /// Release time in milliseconds (50 to 3000)
    /// </summary>
    public float Release { get; set; } = 200f;

    /// <summary>
    /// Threshold in dB (-60 to 0)
    /// </summary>
    public float Threshold { get; set; } = -18f;

    /// <summary>
    /// Compression ratio (1 to 100)
    /// </summary>
    public float Ratio { get; set; } = 4f;

    /// <summary>
    /// Pre-delay in milliseconds (0 to 4)
    /// </summary>
    public float Predelay { get; set; } = 3f;

    protected override IWaveSource ApplyDmoEffect(IWaveSource source)
    {
        return new DmoCompressorEffect(source)
        {
            Gain = Math.Clamp(Gain, -60f, 60f),
            Attack = Math.Clamp(Attack, 0.01f, 500f),
            Release = Math.Clamp(Release, 50f, 3000f),
            Threshold = Math.Clamp(Threshold, -60f, 0f),
            Ratio = Math.Clamp(Ratio, 1f, 100f),
            Predelay = Math.Clamp(Predelay, 0f, 4f)
        };
    }
}
