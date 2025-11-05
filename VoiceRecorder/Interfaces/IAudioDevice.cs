using CSCore.CoreAudioAPI;

namespace VoiceRecorder.Interfaces;

public interface IAudioDevice : IDisposable
{
    Task<IReadOnlyList<string>> GetAvailableDevicesAsync(CancellationToken cancellationToken = default);
    Task<string?> GetDefaultDeviceNameAsync(CancellationToken cancellationToken = default);
    Task<MMDevice> SelectDeviceAsync(string deviceName, CancellationToken cancellationToken = default);
}
