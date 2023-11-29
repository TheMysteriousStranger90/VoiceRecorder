using CSCore;

namespace VoiceRecorder.Filters.Interfaces;

public interface IAudioFilter
{
    IWaveSource ApplyFilter(IWaveSource source);
}