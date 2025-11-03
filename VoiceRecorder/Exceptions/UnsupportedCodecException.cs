namespace VoiceRecorder.Exceptions;

public class UnsupportedCodecException : Exception
{
    public UnsupportedCodecException()
    {
    }

    public UnsupportedCodecException(string message)
        : base(message)
    {
    }

    public UnsupportedCodecException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
