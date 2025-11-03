using CSCore;
using CSCore.Streams.Effects;
using VoiceRecorder.Filters.Interfaces;

namespace VoiceRecorder.Filters;

public class FlangerFilter : IAudioFilter
{
    public IWaveSource ApplyFilter(IWaveSource source)
    {
        var flangerEffect = new DmoFlangerEffect(source);

        flangerEffect.WetDryMix = 50f;
        flangerEffect.Depth = 80f;
        flangerEffect.Feedback = 50f;
        flangerEffect.Frequency = 0.25f;
        flangerEffect.Delay = 2f;
        flangerEffect.Waveform = FlangerWaveform.Sin;

        return flangerEffect;
    }
}
