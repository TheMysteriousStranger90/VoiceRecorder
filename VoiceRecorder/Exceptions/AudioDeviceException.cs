using VoiceRecorder.Models.Enums;

namespace VoiceRecorder.Exceptions;

public class AudioDeviceException : Exception
{
    public AudioDeviceErrorType ErrorType { get; }

    public AudioDeviceException()
        : base(GetDefaultMessage(AudioDeviceErrorType.Unknown))
    {
        ErrorType = AudioDeviceErrorType.Unknown;
    }

    public AudioDeviceException(string message)
        : base(message)
    {
        ErrorType = AudioDeviceErrorType.Unknown;
    }

    public AudioDeviceException(string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorType = AudioDeviceErrorType.Unknown;
    }

    public AudioDeviceException(AudioDeviceErrorType errorType)
        : base(GetDefaultMessage(errorType))
    {
        ErrorType = errorType;
    }

    public AudioDeviceException(AudioDeviceErrorType errorType, string message)
        : base(message)
    {
        ErrorType = errorType;
    }

    public AudioDeviceException(AudioDeviceErrorType errorType, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorType = errorType;
    }

    private static string GetDefaultMessage(AudioDeviceErrorType errorType) => errorType switch
    {
        AudioDeviceErrorType.DeviceNotFound => "Audio device not found",
        AudioDeviceErrorType.DeviceDisabled => "Audio device is disabled",
        AudioDeviceErrorType.DeviceInUse => "Audio device is in use by another application",
        AudioDeviceErrorType.AccessDenied => "Access to audio device was denied",
        AudioDeviceErrorType.InitializationFailed => "Failed to initialize audio device",
        _ => "Unknown audio device error"
    };
}
