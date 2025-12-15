using CSCore;
using CSCore.Streams.Effects;

namespace VoiceRecorder.Filters;

internal sealed class DistortionFilter : DmoFilterBase
{
    protected override IWaveSource ApplyDmoEffect(IWaveSource source)
    {
        return new DmoDistortionEffect(source)
        {
            Gain = -15,
            PostEQCenterFrequency = 300f,
            PostEQBandwidth = 2000f
        };
    }
}
