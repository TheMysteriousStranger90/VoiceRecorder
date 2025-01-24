using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CSCore.CoreAudioAPI;

namespace VoiceRecorder.Models;

public sealed class AudioDevice : IDisposable
{
    private MMDeviceEnumerator _mmdeviceEnumerator;
    private bool _disposed = false;

    public AudioDevice()
    {
        try
        {
            _mmdeviceEnumerator = new MMDeviceEnumerator();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error initializing MMDeviceEnumerator: {ex.Message}");
            throw;
        }
    }

    public List<string> GetAvailableDevices()
    {
        try
        {
            return _mmdeviceEnumerator.EnumAudioEndpoints(DataFlow.Capture, DeviceState.Active)
                .Select(device => device.FriendlyName)
                .ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error enumerating devices: {ex.Message}");
            return new List<string>();
        }
    }

    public MMDevice SelectDevice(string deviceName)
    {
        try
        {
            return _mmdeviceEnumerator.EnumAudioEndpoints(DataFlow.Capture, DeviceState.Active)
                .FirstOrDefault(device => device.FriendlyName == deviceName);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error selecting device: {ex.Message}");
            return null;
        }
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