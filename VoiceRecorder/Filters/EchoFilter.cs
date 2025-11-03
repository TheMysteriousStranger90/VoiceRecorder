using CSCore;
using CSCore.Streams.Effects;
using VoiceRecorder.Filters.Interfaces;

namespace VoiceRecorder.Filters;

public class EchoFilter : IAudioFilter
{
    public IWaveSource ApplyFilter(IWaveSource source)
    {
        var echoEffect = new DmoEchoEffect(source);

        echoEffect.WetDryMix = 60f;
        echoEffect.Feedback = 60f;
        echoEffect.LeftDelay = 500f;
        echoEffect.RightDelay = 500f;

        return echoEffect;
    }
}
