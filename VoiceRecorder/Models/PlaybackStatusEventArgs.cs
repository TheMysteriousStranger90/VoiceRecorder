using System;

namespace VoiceRecorder.Models;

public class PlaybackStatusEventArgs : EventArgs
{
    public PlaybackState State { get; }
    public string CurrentFile { get; }
    public string ErrorMessage { get; }
    
    public PlaybackStatusEventArgs(PlaybackState state, string currentFile)
    {
        State = state;
        CurrentFile = currentFile;
        ErrorMessage = null;
    }
    
    public PlaybackStatusEventArgs(string errorMessage, string currentFile = null)
    {
        State = PlaybackState.Stopped;
        CurrentFile = currentFile;
        ErrorMessage = errorMessage;
    }
}
