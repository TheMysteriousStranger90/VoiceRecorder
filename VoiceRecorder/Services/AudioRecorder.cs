using System.Diagnostics;
using CSCore;
using CSCore.Codecs.WAV;
using CSCore.CoreAudioAPI;
using CSCore.SoundIn;
using CSCore.Streams;
using VoiceRecorder.Exceptions;
using VoiceRecorder.Filters.Interfaces;
using VoiceRecorder.Interfaces;
using VoiceRecorder.Models;
using VoiceRecorder.Models.Enums;

namespace VoiceRecorder.Services;

internal sealed class AudioRecorder : IAudioRecorder
{
    private WasapiCapture? _capture;
    private WaveWriter? _writer;
    private SoundInSource? _soundInSource;
    private IWaveSource? _processedSource;
    private MMDevice? _currentDevice;

    private readonly SemaphoreSlim _recordingLock = new(1, 1);
    private readonly object _writerLock = new();
    private bool _disposed;
    private volatile bool _isWriting;
    private byte[]? _readBuffer;

    public IWaveSource? CaptureSource => _soundInSource;
    public bool IsRecording { get; private set; }

    public event EventHandler? RecordingStarted;
    public event EventHandler<AudioDataEventArgs>? AudioDataAvailable;

    public async Task SetDeviceAsync(MMDevice device)
    {
        await _recordingLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsRecording)
            {
                throw new InvalidOperationException("Cannot change device while recording.");
            }

            _currentDevice = device;

            if (device.DeviceState != DeviceState.Active)
            {
                var errorType = device.DeviceState switch
                {
                    DeviceState.Disabled => AudioDeviceErrorType.DeviceDisabled,
                    DeviceState.NotPresent => AudioDeviceErrorType.DeviceNotFound,
                    DeviceState.UnPlugged => AudioDeviceErrorType.DeviceNotFound,
                    _ => AudioDeviceErrorType.Unknown
                };

                var userMessage = device.DeviceState switch
                {
                    DeviceState.Disabled =>
                        $"Microphone '{device.FriendlyName}' is disabled. Please enable it in Windows Sound Settings.",
                    DeviceState.NotPresent => $"Microphone '{device.FriendlyName}' is not connected.",
                    DeviceState.UnPlugged => $"Microphone '{device.FriendlyName}' is unplugged.",
                    _ => $"Microphone '{device.FriendlyName}' is not available (State: {device.DeviceState})."
                };

                throw new AudioDeviceException(errorType, userMessage);
            }

            Debug.WriteLine($"Device set: {device.FriendlyName}");
        }
        finally
        {
            _recordingLock.Release();
        }
    }

    public async Task StartRecordingAsync(
        string outputFilePath,
        IAudioFilter? filter,
        AudioSettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        await _recordingLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsRecording)
            {
                throw new InvalidOperationException("Recording is already in progress.");
            }

            if (_currentDevice == null)
            {
                throw new InvalidOperationException("Device not set. Call SetDeviceAsync first.");
            }

            CleanupRecordingResources();

            _capture = new WasapiCapture(false, AudioClientShareMode.Shared, 100)
            {
                Device = _currentDevice
            };
            _capture.Initialize();

            Debug.WriteLine($"Capture initialized: {_currentDevice.FriendlyName}, Format: {_capture.WaveFormat}");

            _soundInSource = new SoundInSource(_capture) { FillWithZeros = false };

            _processedSource = BuildProcessingChain(_soundInSource, filter, settings);

            _readBuffer = new byte[_processedSource.WaveFormat.BytesPerSecond / 2];

            try
            {
                _writer = new WaveWriter(outputFilePath, _processedSource.WaveFormat);
                Debug.WriteLine($"WaveWriter created: {outputFilePath}, Format: {_processedSource.WaveFormat}");
            }
            catch (IOException ex)
            {
                throw new AudioRecorderException($"Failed to create output file: {outputFilePath}", ex);
            }

            _soundInSource.DataAvailable += OnDataAvailable;

            _isWriting = true;
            IsRecording = true;

            _capture.Start();

            Debug.WriteLine("Recording started successfully");

            RecordingStarted?.Invoke(this, EventArgs.Empty);
        }
        catch (AudioDeviceException)
        {
            CleanupRecordingResources();
            IsRecording = false;
            _isWriting = false;
            throw;
        }
        catch (AudioRecorderException)
        {
            CleanupRecordingResources();
            IsRecording = false;
            _isWriting = false;
            throw;
        }
        catch (InvalidOperationException)
        {
            CleanupRecordingResources();
            IsRecording = false;
            _isWriting = false;
            throw;
        }
        catch (CoreAudioAPIException ex)
        {
            Debug.WriteLine($"CoreAudioAPI error: {ex.Message}, ErrorCode: {ex.ErrorCode}");
            CleanupRecordingResources();
            IsRecording = false;
            _isWriting = false;

            var (errorType, userMessage) = ClassifyCoreAudioException(ex, _currentDevice?.FriendlyName ?? "Unknown");
            throw new AudioDeviceException(errorType, userMessage, ex);
        }
        catch (Exception ex)
        {
            CleanupRecordingResources();
            IsRecording = false;
            _isWriting = false;
            throw new AudioRecorderException("Failed to start recording", ex);
        }
        finally
        {
            _recordingLock.Release();
        }
    }

    private IWaveSource BuildProcessingChain(SoundInSource source, IAudioFilter? filter, AudioSettings? settings)
    {
        IWaveSource processedSource = source;

        if (settings != null && settings.IsValid())
        {
            processedSource = processedSource
                .ChangeSampleRate(settings.SampleRate)
                .ToSampleSource()
                .ToWaveSource(settings.BitsPerSample);

            processedSource = settings.Channels == 1
                ? processedSource.ToMono()
                : processedSource.ToStereo();
        }

        if (filter != null)
        {
            processedSource = filter.ApplyFilter(processedSource);
        }

        return processedSource;
    }

    private void OnDataAvailable(object? sender, DataAvailableEventArgs e)
    {
        if (!_isWriting || _processedSource == null || _readBuffer == null)
        {
            return;
        }

        try
        {
            int bytesRead;

            while (_isWriting && (bytesRead = _processedSource.Read(_readBuffer, 0, _readBuffer.Length)) > 0)
            {
                lock (_writerLock)
                {
                    if (_writer != null && !_writer.IsDisposed && _isWriting)
                    {
                        _writer.Write(_readBuffer, 0, bytesRead);
                    }
                    else
                    {
                        break;
                    }
                }

                NotifyAudioDataAvailable(_readBuffer, bytesRead);
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in OnDataAvailable: {ex.Message}");
        }
    }

    private void NotifyAudioDataAvailable(byte[] buffer, int bytesRead)
    {
        if (AudioDataAvailable == null || _processedSource == null || bytesRead == 0)
        {
            return;
        }

        try
        {
            float[] samples = ConvertBytesToFloatSamples(buffer, bytesRead, _processedSource.WaveFormat);
            AudioDataAvailable.Invoke(this, new AudioDataEventArgs(samples));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error notifying audio data: {ex.Message}");
        }
    }

    private static float[] ConvertBytesToFloatSamples(byte[] buffer, int count, WaveFormat format)
    {
        int bytesPerSample = format.BitsPerSample / 8;

        if (bytesPerSample == 0)
        {
            return [];
        }

        int sampleCount = count / bytesPerSample;

        if (sampleCount == 0)
        {
            return [];
        }

        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            int byteIndex = i * bytesPerSample;

            switch (format.BitsPerSample)
            {
                case 8 when byteIndex < count:
                {
                    byte sample = buffer[byteIndex];
                    samples[i] = (sample - 128) / 128f;
                    break;
                }
                case 16 when byteIndex + 1 < count:
                {
                    short sample = (short)(buffer[byteIndex] | (buffer[byteIndex + 1] << 8));
                    samples[i] = sample / 32768f;
                    break;
                }
                case 24 when byteIndex + 2 < count:
                {
                    int sample = buffer[byteIndex]
                                 | (buffer[byteIndex + 1] << 8)
                                 | (buffer[byteIndex + 2] << 16);

                    if ((sample & 0x800000) != 0)
                    {
                        sample |= unchecked((int)0xFF000000);
                    }

                    samples[i] = sample / 8388608f;
                    break;
                }
                case 32 when format.WaveFormatTag == AudioEncoding.IeeeFloat && byteIndex + 3 < count:
                {
                    samples[i] = BitConverter.ToSingle(buffer, byteIndex);
                    break;
                }
                case 32 when byteIndex + 3 < count:
                {
                    int sample = BitConverter.ToInt32(buffer, byteIndex);
                    samples[i] = sample / 2147483648f;
                    break;
                }
                default:
                {
                    samples[i] = 0f;
                    break;
                }
            }
        }

        return samples;
    }

    public async Task StopRecordingAsync(CancellationToken cancellationToken = default)
    {
        await _recordingLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsRecording)
            {
                return;
            }

            _isWriting = false;
            IsRecording = false;

            if (_capture != null)
            {
                try
                {
                    _capture.Stop();
                    Debug.WriteLine("Capture stopped");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error stopping capture: {ex.Message}");
                }
            }

            await Task.Delay(50, CancellationToken.None).ConfigureAwait(false);

            lock (_writerLock)
            {
                if (_writer != null && !_writer.IsDisposed)
                {
                    try
                    {
                        _writer.Dispose();
                        Debug.WriteLine("WaveWriter disposed");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error disposing writer: {ex.Message}");
                    }

                    _writer = null;
                }
            }

            CleanupRecordingResources();

            Debug.WriteLine("Recording stopped successfully");
        }
        finally
        {
            _recordingLock.Release();
        }
    }

    private void CleanupRecordingResources()
    {
        _isWriting = false;

        if (_soundInSource != null)
        {
            _soundInSource.DataAvailable -= OnDataAvailable;
        }

        if (_capture != null)
        {
            try
            {
                _capture.Stop();
            }
            catch
            {
                // Ignore
            }

            _capture.Dispose();
            _capture = null;
        }

        _processedSource?.Dispose();
        _processedSource = null;

        _soundInSource?.Dispose();
        _soundInSource = null;

        _readBuffer = null;

        lock (_writerLock)
        {
            if (_writer != null && !_writer.IsDisposed)
            {
                try
                {
                    _writer.Dispose();
                }
                catch
                {
                    // Ignore
                }

                _writer = null;
            }
        }
    }

    private static (AudioDeviceErrorType errorType, string message) ClassifyCoreAudioException(
        CoreAudioAPIException ex, string deviceName)
    {
        return ex.ErrorCode switch
        {
            unchecked((int)0x88890004) => (
                AudioDeviceErrorType.DeviceDisabled,
                $"Microphone '{deviceName}' was disconnected or disabled."),

            unchecked((int)0x88890017) => (
                AudioDeviceErrorType.DeviceInUse,
                $"Microphone '{deviceName}' is being used by another application."),

            unchecked((int)0x80070005) => (
                AudioDeviceErrorType.AccessDenied,
                $"Access to microphone '{deviceName}' was denied."),

            _ => (
                AudioDeviceErrorType.InitializationFailed,
                $"Failed to initialize microphone '{deviceName}'. Error: {ex.Message}")
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CleanupRecordingResources();
        _recordingLock.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
