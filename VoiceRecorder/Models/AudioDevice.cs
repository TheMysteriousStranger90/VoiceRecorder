using System;
using System.Collections.Generic;
using System.Linq;
using CSCore.CoreAudioAPI;

namespace VoiceRecorder.Models;

public sealed class AudioDevice : IDisposable
{
    private MMDeviceEnumerator _mmdeviceEnumerator;
    private bool _disposed = false;

    public AudioDevice()
    {
        _mmdeviceEnumerator = new MMDeviceEnumerator();
    }

    public List<string> GetAvailableDevices()
    {
        return _mmdeviceEnumerator.EnumAudioEndpoints(DataFlow.Capture, DeviceState.Active)
            .Select(device => device.FriendlyName)
            .ToList();
    }

    public MMDevice SelectDevice(string deviceName)
    {
        return _mmdeviceEnumerator.EnumAudioEndpoints(DataFlow.Capture, DeviceState.Active)
            .FirstOrDefault(device => device.FriendlyName == deviceName);
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