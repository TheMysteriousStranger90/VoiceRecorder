using CSCore;
using CSCore.Streams.Effects;
using VoiceRecorder.Filters.Interfaces;

namespace VoiceRecorder.Filters;

internal sealed class FlangerFilter : IAudioFilter
{
    public IWaveSource ApplyFilter(IWaveSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var flangerEffect = new DmoFlangerEffect(source)
        {
            WetDryMix = 30f,
            Depth = 50f,
            Feedback = 30f,
            Frequency = 0.5f,
            Delay = 1.5f,
            Waveform = FlangerWaveform.Sin
        };

        return flangerEffect;
    }
}
