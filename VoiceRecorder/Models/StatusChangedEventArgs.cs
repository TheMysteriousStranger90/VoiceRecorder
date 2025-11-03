namespace VoiceRecorder.Models;

internal sealed class StatusChangedEventArgs : EventArgs
{
    public string Message { get; }

    public StatusChangedEventArgs(string message)
    {
        Message = message ?? string.Empty;
    }
}
