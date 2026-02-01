using System.Diagnostics.CodeAnalysis;

namespace VoiceRecorder.Models.Events;

[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "EventArgs used across assemblies")]
public sealed class StatusChangedEventArgs : EventArgs
{
    public string Message { get; }

    public StatusChangedEventArgs(string message)
    {
        Message = message ?? string.Empty;
    }
}
