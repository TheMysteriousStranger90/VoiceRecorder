using System.Diagnostics.CodeAnalysis;

namespace VoiceRecorder.Models.Enums;

[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Model used across assemblies")]
public enum AudioDeviceChangeType
{
    Added,
    Removed,
    StateChanged,
    DefaultChanged
}
