using System.Diagnostics.CodeAnalysis;
using CSCore.Streams.Effects;

namespace VoiceRecorder.Filters;

/// <summary>
/// Predefined filter configurations for common use cases
/// </summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Filter used across assemblies")]
internal static class FilterPresets
{
    /// <summary>
    /// Small room reverb - intimate sound
    /// </summary>
    public static ReverbFilter SmallRoom => new()
    {
        InGain = -2f,
        ReverbMix = -10f,
        ReverbTime = 500f,
        HighFreqRTRatio = 0.7f
    };

    /// <summary>
    /// Large hall reverb - spacious sound
    /// </summary>
    public static ReverbFilter LargeHall => new()
    {
        InGain = -3f,
        ReverbMix = -4f,
        ReverbTime = 2000f,
        HighFreqRTRatio = 0.5f
    };

    /// <summary>
    /// Cathedral reverb - very long decay
    /// </summary>
    public static ReverbFilter Cathedral => new()
    {
        InGain = -4f,
        ReverbMix = -3f,
        ReverbTime = 2800f,
        HighFreqRTRatio = 0.4f
    };

    /// <summary>
    /// Subtle echo - slight repetition
    /// </summary>
    public static EchoFilter SubtleEcho => new()
    {
        WetDryMix = 20f,
        Feedback = 20f,
        LeftDelay = 150f,
        RightDelay = 180f
    };

    /// <summary>
    /// Slapback echo - rockabilly style
    /// </summary>
    public static EchoFilter SlapbackEcho => new()
    {
        WetDryMix = 45f,
        Feedback = 10f,
        LeftDelay = 80f,
        RightDelay = 80f
    };

    /// <summary>
    /// Long delay echo - spacey effect
    /// </summary>
    public static EchoFilter LongEcho => new()
    {
        WetDryMix = 35f,
        Feedback = 50f,
        LeftDelay = 400f,
        RightDelay = 450f
    };

    /// <summary>
    /// Rich chorus - thick, lush sound
    /// </summary>
    public static ChorusFilter RichChorus => new()
    {
        WetDryMix = 60f,
        Depth = 35f,
        Feedback = 25f,
        Frequency = 0.5f,
        Delay = 15f
    };

    /// <summary>
    /// Subtle chorus - light enhancement
    /// </summary>
    public static ChorusFilter SubtleChorus => new()
    {
        WetDryMix = 30f,
        Depth = 15f,
        Feedback = 10f,
        Frequency = 0.8f,
        Delay = 8f
    };

    /// <summary>
    /// Jet flanger - classic sweeping sound
    /// </summary>
    public static FlangerFilter JetFlanger => new()
    {
        WetDryMix = 50f,
        Depth = 80f,
        Feedback = 40f,
        Frequency = 0.2f,
        Delay = 3f
    };

    /// <summary>
    /// Gentle flanger - subtle movement
    /// </summary>
    public static FlangerFilter GentleFlanger => new()
    {
        WetDryMix = 35f,
        Depth = 40f,
        Feedback = 20f,
        Frequency = 0.5f,
        Delay = 1.5f
    };

    /// <summary>
    /// Light distortion - adds warmth
    /// </summary>
    public static DistortionFilter WarmDistortion => new()
    {
        Gain = -25f,
        Edge = 30f,
        PostEQCenterFrequency = 2000f,
        PostEQBandwidth = 3000f,
        PreLowpassCutoff = 6000f
    };

    /// <summary>
    /// Heavy distortion - aggressive sound
    /// </summary>
    public static DistortionFilter HeavyDistortion => new()
    {
        Gain = -10f,
        Edge = 80f,
        PostEQCenterFrequency = 3000f,
        PostEQBandwidth = 2000f,
        PreLowpassCutoff = 8000f
    };

    /// <summary>
    /// Voice compressor - for podcasts/vocals
    /// </summary>
    public static CompressorFilter VoiceCompressor => new()
    {
        Gain = 6f,
        Attack = 15f,
        Release = 150f,
        Threshold = -20f,
        Ratio = 4f,
        Predelay = 2f
    };

    /// <summary>
    /// Gentle compression - subtle leveling
    /// </summary>
    public static CompressorFilter GentleCompression => new()
    {
        Gain = 3f,
        Attack = 25f,
        Release = 250f,
        Threshold = -15f,
        Ratio = 2f,
        Predelay = 1f
    };

    /// <summary>
    /// Heavy compression - broadcast style
    /// </summary>
    public static CompressorFilter BroadcastCompressor => new()
    {
        Gain = 8f,
        Attack = 5f,
        Release = 100f,
        Threshold = -25f,
        Ratio = 8f,
        Predelay = 3f
    };

    /// <summary>
    /// Robot voice effect
    /// </summary>
    public static GargleFilter RobotVoice => new()
    {
        RateHz = 100,
        WaveShape = GargleWaveShape.Square
    };

    /// <summary>
    /// Tremolo effect
    /// </summary>
    public static GargleFilter Tremolo => new()
    {
        RateHz = 8,
        WaveShape = GargleWaveShape.Triangle
    };

    /// <summary>
    /// Professional vocal chain: compression + subtle reverb
    /// </summary>
    public static ChainFilter ProfessionalVocal => new ChainFilter()
        .AddFilter(VoiceCompressor)
        .AddFilter(SmallRoom);

    /// <summary>
    /// Atmospheric chain: chorus + reverb + echo
    /// </summary>
    public static ChainFilter Atmospheric => new ChainFilter()
        .AddFilter(SubtleChorus)
        .AddFilter(LargeHall)
        .AddFilter(SubtleEcho);

    /// <summary>
    /// Vintage radio effect
    /// </summary>
    public static ChainFilter VintageRadio => new ChainFilter()
        .AddFilter(WarmDistortion)
        .AddFilter(BroadcastCompressor);
}
