using System.Diagnostics;
using CSCore.CoreAudioAPI;
using VoiceRecorder.Exceptions;
using VoiceRecorder.Interfaces;
using VoiceRecorder.Models;
using VoiceRecorder.Models.Enums;
using VoiceRecorder.Models.Events;

namespace VoiceRecorder.Services;

internal sealed class AudioDevice : IAudioDevice
{
    private MMDeviceEnumerator? _mmdeviceEnumerator;
    private bool _disposed;
    private readonly SemaphoreSlim _deviceLock = new(1, 1);
    private MMNotificationClient? _notificationClient;

    public event EventHandler<AudioDeviceChangedEventArgs>? DeviceChanged;
    public event EventHandler<AudioDeviceChangedEventArgs>? DeviceRemoved;
    public event EventHandler<AudioDeviceChangedEventArgs>? DefaultDeviceChanged;

    public AudioDevice()
    {
        try
        {
            _mmdeviceEnumerator = new MMDeviceEnumerator();
            InitializeDeviceNotifications();
        }
        catch (CoreAudioAPIException ex)
        {
            Debug.WriteLine($"Error initializing MMDeviceEnumerator: {ex.Message}");
            throw new AudioDeviceException(
                AudioDeviceErrorType.InitializationFailed,
                "Failed to initialize audio device enumerator. Please check your audio drivers.",
                ex);
        }
    }

    private void InitializeDeviceNotifications()
    {
        try
        {
            _notificationClient = new MMNotificationClient(_mmdeviceEnumerator!);
            _notificationClient.DeviceAdded += OnDeviceAdded;
            _notificationClient.DeviceRemoved += OnDeviceRemoved;
            _notificationClient.DeviceStateChanged += OnDeviceStateChanged;
            _notificationClient.DefaultDeviceChanged += OnDefaultDeviceChanged;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize device notifications: {ex.Message}");
        }
    }

    private void OnDeviceAdded(object? sender, DeviceNotificationEventArgs e)
    {
        Debug.WriteLine($"Device added: {e.DeviceId}");
        DeviceChanged?.Invoke(this, new AudioDeviceChangedEventArgs(e.DeviceId, AudioDeviceChangeType.Added));
    }

    private void OnDeviceRemoved(object? sender, DeviceNotificationEventArgs e)
    {
        Debug.WriteLine($"Device removed: {e.DeviceId}");
        DeviceRemoved?.Invoke(this, new AudioDeviceChangedEventArgs(e.DeviceId, AudioDeviceChangeType.Removed));
    }

    private void OnDeviceStateChanged(object? sender, DeviceStateChangedEventArgs e)
    {
        Debug.WriteLine($"Device state changed: {e.DeviceId} -> {e.DeviceState}");
        DeviceChanged?.Invoke(this, new AudioDeviceChangedEventArgs(e.DeviceId, AudioDeviceChangeType.StateChanged));
    }

    private void OnDefaultDeviceChanged(object? sender, DefaultDeviceChangedEventArgs e)
    {
        if (e.DataFlow == DataFlow.Capture)
        {
            Debug.WriteLine($"Default capture device changed: {e.DeviceId}");
            DefaultDeviceChanged?.Invoke(this,
                new AudioDeviceChangedEventArgs(e.DeviceId, AudioDeviceChangeType.DefaultChanged));
        }
    }

    public async Task<IReadOnlyList<AudioDeviceInfo>> GetAvailableDevicesWithInfoAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _deviceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                var deviceInfoList = new List<AudioDeviceInfo>();

                try
                {
                    if (_mmdeviceEnumerator != null)
                    {
                        var devices = _mmdeviceEnumerator
                            .EnumAudioEndpoints(DataFlow.Capture, DeviceState.Active)
                            .ToList();

                        string? defaultDeviceId = null;
                        try
                        {
                            var defaultDevice =
                                _mmdeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
                            defaultDeviceId = defaultDevice.DeviceID;
                        }
                        catch (CoreAudioAPIException)
                        {
                            // No default device available
                        }

                        foreach (var device in devices)
                        {
                            try
                            {
                                var deviceInfo = new AudioDeviceInfo
                                {
                                    Id = device.DeviceID,
                                    Name = device.FriendlyName,
                                    State = device.DeviceState,
                                    IsDefault = device.DeviceID == defaultDeviceId,
                                    SampleRate = GetDeviceSampleRate(device),
                                    Channels = GetDeviceChannels(device)
                                };
                                deviceInfoList.Add(deviceInfo);

                                Debug.WriteLine(
                                    $"Found device: {deviceInfo.Name} (Default: {deviceInfo.IsDefault}, {deviceInfo.SampleRate}Hz, {deviceInfo.Channels}ch)");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error getting device info: {ex.Message}");
                            }
                        }
                    }
                }
                catch (CoreAudioAPIException ex)
                {
                    Debug.WriteLine($"Error enumerating devices: {ex.Message}");
                }

                return (IReadOnlyList<AudioDeviceInfo>)deviceInfoList;
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _deviceLock.Release();
        }
    }

    private static int GetDeviceSampleRate(MMDevice device)
    {
        try
        {
            using var audioClient = AudioClient.FromMMDevice(device);
            var format = audioClient.GetMixFormat();
            return format.SampleRate;
        }
        catch
        {
            return 44100;
        }
    }

    private static int GetDeviceChannels(MMDevice device)
    {
        try
        {
            using var audioClient = AudioClient.FromMMDevice(device);
            var format = audioClient.GetMixFormat();
            return format.Channels;
        }
        catch
        {
            return 2;
        }
    }

    public async Task<IReadOnlyList<string>> GetAvailableDevicesAsync(CancellationToken cancellationToken = default)
    {
        var devices = await GetAvailableDevicesWithInfoAsync(cancellationToken).ConfigureAwait(false);
        return devices.Select(d => d.Name).ToList();
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

                            throw new AudioDeviceException(
                                AudioDeviceErrorType.DeviceNotFound,
                                $"Microphone '{deviceName}' not found. It may have been disconnected.");
                        }

                        ValidateDeviceState(device);

                        Debug.WriteLine($"Selected device: {device.FriendlyName}");
                        Debug.WriteLine($"Device ID: {device.DeviceID}");
                        Debug.WriteLine($"Device State: {device.DeviceState}");
                        Debug.WriteLine($"Is Default: {IsDefaultDevice(device)}");

                        return device;
                    }
                }
                catch (AudioDeviceException)
                {
                    throw;
                }
                catch (CoreAudioAPIException ex)
                {
                    Debug.WriteLine($"Error selecting device: {ex.Message}");
                    throw new AudioDeviceException(
                        AudioDeviceErrorType.InitializationFailed,
                        $"Failed to access microphone '{deviceName}'. Please check your audio settings.",
                        ex);
                }

                throw new AudioDeviceException(
                    AudioDeviceErrorType.InitializationFailed,
                    "Audio system is not initialized.");
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _deviceLock.Release();
        }
    }

    private static void ValidateDeviceState(MMDevice device)
    {
        if (device.DeviceState != DeviceState.Active)
        {
            var (errorType, message) = device.DeviceState switch
            {
                DeviceState.Disabled => (
                    AudioDeviceErrorType.DeviceDisabled,
                    $"Microphone '{device.FriendlyName}' is disabled. Enable it in Windows Sound Settings."),
                DeviceState.NotPresent => (
                    AudioDeviceErrorType.DeviceNotFound,
                    $"Microphone '{device.FriendlyName}' is not connected."),
                DeviceState.UnPlugged => (
                    AudioDeviceErrorType.DeviceNotFound,
                    $"Microphone '{device.FriendlyName}' is unplugged."),
                _ => (
                    AudioDeviceErrorType.Unknown,
                    $"Microphone '{device.FriendlyName}' is unavailable (State: {device.DeviceState}).")
            };

            throw new AudioDeviceException(errorType, message);
        }
    }

    public async Task<bool> IsDeviceAvailableAsync(string deviceName, CancellationToken cancellationToken = default)
    {
        try
        {
            var devices = await GetAvailableDevicesAsync(cancellationToken).ConfigureAwait(false);
            return devices.Contains(deviceName);
        }
        catch
        {
            return false;
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
                if (_notificationClient != null)
                {
                    _notificationClient.DeviceAdded -= OnDeviceAdded;
                    _notificationClient.DeviceRemoved -= OnDeviceRemoved;
                    _notificationClient.DeviceStateChanged -= OnDeviceStateChanged;
                    _notificationClient.DefaultDeviceChanged -= OnDefaultDeviceChanged;
                    _notificationClient.Dispose();
                    _notificationClient = null;
                }

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
