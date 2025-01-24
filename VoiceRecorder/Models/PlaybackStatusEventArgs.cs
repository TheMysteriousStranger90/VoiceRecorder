using System;

namespace VoiceRecorder.Models;

public class PlaybackStatusEventArgs : EventArgs
{
    public bool IsPlaying { get; }
    public string CurrentFile { get; }
    public string ErrorMessage { get; }

    public PlaybackStatusEventArgs(bool isPlaying, string currentFile, string errorMessage = null)
    {
        IsPlaying = isPlaying;
        CurrentFile = currentFile;
        ErrorMessage = errorMessage;
    }
}