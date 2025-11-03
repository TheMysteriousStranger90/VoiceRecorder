namespace VoiceRecorder.Models;

public sealed class PlaybackStatusEventArgs : EventArgs
{
    public PlaybackState State { get; }
    public string FileName { get; }
    public string ErrorMessage { get; }

    public PlaybackStatusEventArgs(PlaybackState state, string fileName)
    {
        State = state;
        FileName = fileName ?? string.Empty;
        ErrorMessage = string.Empty;
    }

    public PlaybackStatusEventArgs(string errorMessage, string fileName)
    {
        State = PlaybackState.Stopped;
        FileName = fileName ?? string.Empty;
        ErrorMessage = errorMessage ?? string.Empty;
    }
}
