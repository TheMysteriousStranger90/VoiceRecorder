using CSCore;
using CSCore.Streams.Effects;
using VoiceRecorder.Filters.Interfaces;

namespace VoiceRecorder.Filters;

public class CompressorFilter : IAudioFilter
{
    public IWaveSource ApplyFilter(IWaveSource source)
    {
        return new DmoCompressorEffect(source);
    }
}