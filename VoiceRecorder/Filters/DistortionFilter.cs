using CSCore;
using CSCore.Streams.Effects;
using VoiceRecorder.Filters.Interfaces;

namespace VoiceRecorder.Filters;

public class CompressorFilter : IAudioFilter
{
    public IWaveSource ApplyFilter(IWaveSource source)
    {
        var compressor = new DmoCompressorEffect(source);
        compressor.Attack = 10;
        compressor.Gain = 15;
        compressor.Predelay = 4;
        compressor.Release = 200;
        compressor.Threshold = -20;
        compressor.Ratio = 3;
        return compressor;
    }
}