using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CSCore;
using VoiceRecorder.Filters.Interfaces;

namespace VoiceRecorder.Filters;

[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Filter used across assemblies")]
public abstract class DmoFilterBase : IAudioFilter
{
    public abstract string Name { get; }
    public abstract string Description { get; }

    public IWaveSource ApplyFilter(IWaveSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var format = source.WaveFormat;
        Debug.WriteLine(
            $"{GetType().Name} input: {format.SampleRate}Hz, {format.BitsPerSample}bit, {format.Channels}ch");

        IWaveSource compatibleSource = source;

        if (format.BitsPerSample != 16 && format.BitsPerSample != 32)
        {
            compatibleSource = source.ToSampleSource().ToWaveSource(16);
            Debug.WriteLine($"{GetType().Name}: Converted to 16-bit for DMO compatibility");
        }

        if (format.Channels == 1)
        {
            compatibleSource = compatibleSource.ToStereo();
            Debug.WriteLine($"{GetType().Name}: Converted to stereo for better effect quality");
        }

        try
        {
            return ApplyDmoEffect(compatibleSource);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{GetType().Name} failed: {ex.Message}. Returning original source.");
            return source;
        }
    }

    protected abstract IWaveSource ApplyDmoEffect(IWaveSource source);
}
