using CSCore.CoreAudioAPI;

namespace VoiceRecorder.Interfaces;

public interface IAudioDevice : IDisposable
{
    IReadOnlyList<string> GetAvailableDevices();
    string? GetDefaultDeviceName();
    MMDevice SelectDevice(string deviceName);
}
