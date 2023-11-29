using System;
using CSCore;
using CSCore.Streams.Effects;
using VoiceRecorder.Filters.Interfaces;

namespace VoiceRecorder.Filters;

public class ChorusFilter : IAudioFilter
{
    public IWaveSource ApplyFilter(IWaveSource source)
    {
        return new DmoChorusEffect(source);
    }
}