using System.Diagnostics;
using CSCore.CoreAudioAPI;
using VoiceRecorder.Interfaces;

namespace VoiceRecorder.Services;

internal sealed class AudioDevice : IAudioDevice
{
    private MMDeviceEnumerator? _mmdeviceEnumerator;
    private bool _disposed;

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

    public IReadOnlyList<string> GetAvailableDevices()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

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

                return devices.Select(device => device.FriendlyName).ToList();
            }
        }
        catch (CoreAudioAPIException ex)
        {
            Debug.WriteLine($"Error enumerating devices: {ex.Message}");
        }

        return Array.Empty<string>();
    }

    public string? GetDefaultDeviceName()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

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
    }

    public MMDevice SelectDevice(string deviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        ObjectDisposedException.ThrowIf(_disposed, this);

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
