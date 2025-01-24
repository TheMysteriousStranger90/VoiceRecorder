using System;

namespace VoiceRecorder.Exceptions;

public class AudioRecorderException : Exception
{
    public AudioRecorderException(string message, Exception innerException) 
        : base(message, innerException) { }
}