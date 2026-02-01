using System.Diagnostics.CodeAnalysis;
using CSCore;

namespace VoiceRecorder.Filters.Interfaces;

[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Interfaces used across assemblies")]
public interface IAudioFilter
{
    IWaveSource ApplyFilter(IWaveSource source);
}
