using CSCore;
using CSCore.Streams.Effects;
using VoiceRecorder.Filters.Interfaces;

namespace VoiceRecorder.Filters;

internal sealed class CompressorFilter : IAudioFilter
{
    public IWaveSource ApplyFilter(IWaveSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var compressorEffect = new DmoCompressorEffect(source)
        {
            Gain = 5f,
            Attack = 5f,
            Release = 100f,
            Threshold = -20f,
            Ratio = 3f,
            Predelay = 2f
        };

        return compressorEffect;
    }
}
