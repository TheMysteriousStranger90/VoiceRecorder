using CSCore;
using CSCore.DMO.Effects;
using CSCore.Streams.Effects;
using VoiceRecorder.Filters.Interfaces;

namespace VoiceRecorder.Filters;

public class FlangerFilter : IAudioFilter
{
    public IWaveSource ApplyFilter(IWaveSource source)
    {
        return new DmoFlangerEffect(source);
    }
}

