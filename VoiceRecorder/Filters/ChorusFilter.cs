using CSCore;
using CSCore.Streams.Effects;

namespace VoiceRecorder.Filters;

internal sealed class ChorusFilter : DmoFilterBase
{
    protected override IWaveSource ApplyDmoEffect(IWaveSource source)
    {
        return new DmoChorusEffect(source)
        {
            WetDryMix = 40f,
            Depth = 30f,
            Feedback = 20f,
            Frequency = 1.0f,
            Delay = 15f,
            Waveform = ChorusWaveform.WaveformSin
        };
    }
}
