using System.Diagnostics;
using CSCore;
using VoiceRecorder.Filters.Interfaces;

namespace VoiceRecorder.Filters;

internal abstract class DmoFilterBase : IAudioFilter
{
    public IWaveSource ApplyFilter(IWaveSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var format = source.WaveFormat;
        Debug.WriteLine($"{GetType().Name} input: {format.SampleRate}Hz, {format.BitsPerSample}bit, {format.Channels}ch");

        IWaveSource compatibleSource = source;

        if (format.BitsPerSample != 16 && format.BitsPerSample != 32)
        {
            compatibleSource = source.ToSampleSource().ToWaveSource(16);
            Debug.WriteLine($"{GetType().Name}: Converted to 16-bit for DMO compatibility");
        }

        return ApplyDmoEffect(compatibleSource);
    }

    protected abstract IWaveSource ApplyDmoEffect(IWaveSource source);
}
