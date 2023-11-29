using CSCore.CoreAudioAPI;
using System;
using System.Collections.Generic;
using System.Linq;

public class AudioDevice : IDisposable
{
    private MMDeviceEnumerator mmdeviceEnumerator;
    private bool disposed = false;

    public AudioDevice()
    {
        mmdeviceEnumerator = new MMDeviceEnumerator();
    }

    public List<string> GetAvailableDevices()
    {
        return mmdeviceEnumerator.EnumAudioEndpoints(DataFlow.Capture, DeviceState.Active)
            .Select(device => device.FriendlyName)
            .ToList();
    }

    public MMDevice SelectDevice(string deviceName)
    {
        return mmdeviceEnumerator.EnumAudioEndpoints(DataFlow.Capture, DeviceState.Active)
            .FirstOrDefault(device => device.FriendlyName == deviceName);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                if (mmdeviceEnumerator != null)
                {
                    mmdeviceEnumerator.Dispose();
                    mmdeviceEnumerator = null;
                }
            }

            disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}