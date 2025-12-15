using CSCore;
using CSCore.Streams.Effects;

namespace VoiceRecorder.Filters;

internal sealed class EchoFilter : DmoFilterBase
{
    protected override IWaveSource ApplyDmoEffect(IWaveSource source)
    {
        return new DmoEchoEffect(source)
        {
            WetDryMix = 40f,
            Feedback = 35f,
            LeftDelay = 300f,
            RightDelay = 300f
        };
    }
}
