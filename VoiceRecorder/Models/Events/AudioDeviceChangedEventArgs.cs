using System.Diagnostics.CodeAnalysis;
using VoiceRecorder.Models.Enums;

namespace VoiceRecorder.Models.Events;

[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "EventArgs used across assemblies")]
public sealed class AudioDeviceChangedEventArgs : EventArgs
{
    public string DeviceId { get; }
    public AudioDeviceChangeType ChangeType { get; }

    public AudioDeviceChangedEventArgs(string deviceId, AudioDeviceChangeType changeType)
    {
        DeviceId = deviceId;
        ChangeType = changeType;
    }
}
