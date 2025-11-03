using System.Diagnostics;
using CSCore.CoreAudioAPI;

namespace VoiceRecorder.Models;

public sealed class AudioDevice : IDisposable
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
        try
        {
            if (_mmdeviceEnumerator != null)
                return _mmdeviceEnumerator.EnumAudioEndpoints(DataFlow.Capture, DeviceState.Active)
                    .Select(device => device.FriendlyName)
                    .ToList();
        }
        catch (CoreAudioAPIException ex)
        {
            Debug.WriteLine($"Error enumerating devices: {ex.Message}");
        }

        return new List<string>();
    }

    public MMDevice SelectDevice(string deviceName)
    {
        try
        {
            if (_mmdeviceEnumerator != null)
            {
                var device = _mmdeviceEnumerator.EnumAudioEndpoints(DataFlow.Capture, DeviceState.Active)
                    .FirstOrDefault(device => device.FriendlyName == deviceName);

                if (device == null)
                {
                    throw new InvalidOperationException($"Device with name '{deviceName}' not found.");
                }

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

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                if (_mmdeviceEnumerator != null)
                {
                    _mmdeviceEnumerator.Dispose();
                    _mmdeviceEnumerator = null;
                }
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
