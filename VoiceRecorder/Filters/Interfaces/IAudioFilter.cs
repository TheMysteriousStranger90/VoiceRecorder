using System.Diagnostics.CodeAnalysis;
using CSCore;

namespace VoiceRecorder.Filters.Interfaces;

[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Interfaces used across assemblies")]
public interface IAudioFilter
{
    /// <summary>
    /// Display name of the filter
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Description of what the filter does
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Apply the filter to the audio source
    /// </summary>
    IWaveSource ApplyFilter(IWaveSource source);
}
