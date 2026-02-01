namespace VoiceRecorder.Exceptions;

internal sealed class AudioRecorderException : Exception
{
    public AudioRecorderException()
    {
    }

    public AudioRecorderException(string message)
        : base(message)
    {
    }

    public AudioRecorderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
