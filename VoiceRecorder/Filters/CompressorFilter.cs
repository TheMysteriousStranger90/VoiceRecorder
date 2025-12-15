using CSCore;
using CSCore.Streams.Effects;

namespace VoiceRecorder.Filters;

internal sealed class CompressorFilter : DmoFilterBase
{
    protected override IWaveSource ApplyDmoEffect(IWaveSource source)
    {
        return new DmoCompressorEffect(source)
        {
            Gain = 5f,
            Attack = 5f,
            Release = 100f,
            Threshold = -20f,
            Ratio = 3f,
            Predelay = 2f
        };
    }
}
