using CSCore.CoreAudioAPI;

namespace VoiceRecorder.Interfaces;

public interface IAudioDevice : IDisposable
{
    IReadOnlyList<string> GetAvailableDevices();
    MMDevice SelectDevice(string deviceName);
}
