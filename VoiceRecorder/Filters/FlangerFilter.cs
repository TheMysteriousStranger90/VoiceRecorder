using CSCore;
using CSCore.Streams.Effects;

namespace VoiceRecorder.Filters;

internal sealed class FlangerFilter : DmoFilterBase
{
    protected override IWaveSource ApplyDmoEffect(IWaveSource source)
    {
        return new DmoFlangerEffect(source)
        {
            WetDryMix = 30f,
            Depth = 50f,
            Feedback = 30f,
            Frequency = 0.5f,
            Delay = 1.5f,
            Waveform = FlangerWaveform.Sin
        };
    }
}
