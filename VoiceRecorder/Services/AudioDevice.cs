using System.Diagnostics;
using CSCore.CoreAudioAPI;
using VoiceRecorder.Interfaces;

namespace VoiceRecorder.Services;

internal sealed class AudioDevice : IAudioDevice
{
    private MMDeviceEnumerator? _mmdeviceEnumerator;
    private bool _disposed;
    private readonly SemaphoreSlim _deviceLock = new(1, 1);

    public AudioDevice()
    {
        try
        {
            _mmdeviceEnumerator = new MMDeviceEnumerator();
        }
        catch (CoreAudioAPIException ex)
        {
            Debug.WriteLine($"Error initializing MMDeviceEnumerator: {ex.Message}");
            throw;
        }
    }

    public async Task<IReadOnlyList<string>> GetAvailableDevicesAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _deviceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (_mmdeviceEnumerator != null)
                    {
                        var devices = _mmdeviceEnumerator
                            .EnumAudioEndpoints(DataFlow.Capture, DeviceState.Active)
                            .ToList();

                        Debug.WriteLine($"Found {devices.Count} capture devices:");
                        foreach (var device in devices)
                        {
                            Debug.WriteLine($"  - {device.FriendlyName} (ID: {device.DeviceID})");
                        }

                        return (IReadOnlyList<string>)devices.Select(device => device.FriendlyName).ToList();
                    }
                }
                catch (CoreAudioAPIException ex)
                {
                    Debug.WriteLine($"Error enumerating devices: {ex.Message}");
                }

                return Array.Empty<string>();
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _deviceLock.Release();
        }
    }

    public async Task<string?> GetDefaultDeviceNameAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _deviceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (_mmdeviceEnumerator != null)
                    {
                        var defaultDevice = _mmdeviceEnumerator.GetDefaultAudioEndpoint(
                            DataFlow.Capture,
                            Role.Console);

                        Debug.WriteLine($"Default device: {defaultDevice.FriendlyName}");
                        return defaultDevice.FriendlyName;
                    }
                }
                catch (CoreAudioAPIException ex)
                {
                    Debug.WriteLine($"Error getting default device: {ex.Message}");
                }

                return null;
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _deviceLock.Release();
        }
    }

    public async Task<MMDevice> SelectDeviceAsync(string deviceName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _deviceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (_mmdeviceEnumerator != null)
                    {
                        var devices = _mmdeviceEnumerator
                            .EnumAudioEndpoints(DataFlow.Capture, DeviceState.Active)
                            .ToList();

                        var device = devices.FirstOrDefault(d => d.FriendlyName == deviceName);

                        if (device == null)
                        {
                            Debug.WriteLine($"Device '{deviceName}' not found. Available devices:");
                            foreach (var d in devices)
                            {
                                Debug.WriteLine($"  - {d.FriendlyName}");
                            }

                            throw new InvalidOperationException($"Device with name '{deviceName}' not found.");
                        }

                        Debug.WriteLine($"Selected device: {device.FriendlyName}");
                        Debug.WriteLine($"Device ID: {device.DeviceID}");
                        Debug.WriteLine($"Device State: {device.DeviceState}");
                        Debug.WriteLine($"Is Default: {IsDefaultDevice(device)}");

                        return device;
                    }
                }
                catch (CoreAudioAPIException ex)
                {
                    Debug.WriteLine($"Error selecting device: {ex.Message}");
                    throw;
                }

                throw new InvalidOperationException("MMDeviceEnumerator is not initialized.");
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _deviceLock.Release();
        }
    }

    private bool IsDefaultDevice(MMDevice device)
    {
        try
        {
            if (_mmdeviceEnumerator != null)
            {
                var defaultDevice = _mmdeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
                return device.DeviceID == defaultDevice.DeviceID;
            }
        }
        catch (CoreAudioAPIException ex)
        {
            Debug.WriteLine($"Error checking default device: {ex.Message}");
        }

        return false;
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _mmdeviceEnumerator?.Dispose();
                _mmdeviceEnumerator = null;
                _deviceLock.Dispose();
            }

            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
