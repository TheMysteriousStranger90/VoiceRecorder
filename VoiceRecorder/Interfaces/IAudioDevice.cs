using System.Diagnostics.CodeAnalysis;
using CSCore.CoreAudioAPI;
using VoiceRecorder.Models;
using VoiceRecorder.Models.Events;

namespace VoiceRecorder.Interfaces;

[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Interfaces used across assemblies")]
public interface IAudioDevice : IDisposable
{
    Task<IReadOnlyList<string>> GetAvailableDevicesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AudioDeviceInfo>> GetAvailableDevicesWithInfoAsync(CancellationToken cancellationToken = default);
    Task<string?> GetDefaultDeviceNameAsync(CancellationToken cancellationToken = default);
    Task<MMDevice> SelectDeviceAsync(string deviceName, CancellationToken cancellationToken = default);
    Task<bool> IsDeviceAvailableAsync(string deviceName, CancellationToken cancellationToken = default);

    event EventHandler<AudioDeviceChangedEventArgs>? DeviceChanged;
    event EventHandler<AudioDeviceChangedEventArgs>? DeviceRemoved;
    event EventHandler<AudioDeviceChangedEventArgs>? DefaultDeviceChanged;
}
