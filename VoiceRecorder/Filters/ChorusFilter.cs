using CSCore;
using CSCore.Streams.Effects;
using VoiceRecorder.Filters.Interfaces;

namespace VoiceRecorder.Filters;

internal sealed class ChorusFilter : IAudioFilter
{
    public IWaveSource ApplyFilter(IWaveSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var chorusEffect = new DmoChorusEffect(source)
        {
            WetDryMix = 40f,
            Depth = 30f,
            Feedback = 20f,
            Frequency = 1.0f,
            Delay = 15f,
            Waveform = ChorusWaveform.WaveformSin
        };

        return chorusEffect;
    }
}
