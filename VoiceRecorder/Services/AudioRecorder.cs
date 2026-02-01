using System.Diagnostics;
using System.Globalization;
using CSCore;
using CSCore.Codecs.WAV;
using CSCore.CoreAudioAPI;
using CSCore.MediaFoundation;
using CSCore.SoundIn;
using CSCore.Streams;
using VoiceRecorder.Exceptions;
using VoiceRecorder.Filters.Interfaces;
using VoiceRecorder.Interfaces;
using VoiceRecorder.Models;
using VoiceRecorder.Models.Enums;
using VoiceRecorder.Models.Events;

namespace VoiceRecorder.Services;

internal sealed class AudioRecorder : IAudioRecorder
{
    private WasapiCapture? _capture;
    private IDisposable? _writerDisposable;
    private SoundInSource? _soundInSource;
    private IWaveSource? _processedSource;
    private MMDevice? _currentDevice;

    private readonly SemaphoreSlim _recordingLock = new(1, 1);
    private readonly object _writerLock = new();
    private bool _disposed;
    private volatile bool _isWriting;
    private byte[]? _readBuffer;
    private long _bytesWritten;
    private readonly Stopwatch _recordingStopwatch = new();

    // Audio level monitoring
    private float _peakLevel;
    private float _averageLevel;
    private readonly object _levelLock = new();

    public IWaveSource? CaptureSource => _soundInSource;
    public bool IsRecording { get; private set; }

    public float PeakLevel
    {
        get
        {
            lock (_levelLock) return _peakLevel;
        }
    }

    public float AverageLevel
    {
        get
        {
            lock (_levelLock) return _averageLevel;
        }
    }

    public TimeSpan RecordingDuration => _recordingStopwatch.Elapsed;
    public long BytesRecorded => Interlocked.Read(ref _bytesWritten);

    public event EventHandler? RecordingStarted;
    public event EventHandler<AudioDataEventArgs>? AudioDataAvailable;
    public event EventHandler<AudioLevelEventArgs>? AudioLevelChanged;
    public event EventHandler<RecordingProgressEventArgs>? RecordingProgressChanged;

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
            ResetMetrics();

            // Initialize capture with optimized settings
            _capture = new WasapiCapture(false, AudioClientShareMode.Shared, 50)
            {
                Device = _currentDevice
            };
            _capture.Initialize();

            Debug.WriteLine($"Capture initialized: {_currentDevice.FriendlyName}, Format: {_capture.WaveFormat}");

            _soundInSource = new SoundInSource(_capture) { FillWithZeros = false };

            _processedSource = BuildProcessingChain(_soundInSource, filter, settings);

            // Optimize buffer size based on format
            int bufferSize = CalculateOptimalBufferSize(_processedSource.WaveFormat);
            _readBuffer = new byte[bufferSize];

            // Create appropriate writer based on file extension
            _writerDisposable = CreateWriter(outputFilePath, _processedSource.WaveFormat) as IDisposable;

            _soundInSource.DataAvailable += OnDataAvailable;

            _isWriting = true;
            IsRecording = true;
            _bytesWritten = 0;
            _recordingStopwatch.Restart();

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

    private static int CalculateOptimalBufferSize(WaveFormat format)
    {
        int bytesPerSecond = format.BytesPerSecond;
        int bufferSize = bytesPerSecond / 10;

        int bytesPerSample = format.BitsPerSample / 8 * format.Channels;
        bufferSize = (bufferSize / bytesPerSample) * bytesPerSample;

        return Math.Clamp(bufferSize, 4096, 65536);
    }

    private static IWriteable CreateWriter(string filePath, WaveFormat format)
    {
        string extension = Path.GetExtension(filePath).ToUpperInvariant();

        try
        {
            return extension switch
            {
                ".WAV" => new WaveWriter(filePath, format),
                ".MP3" => CreateMp3Encoder(filePath, format),
                ".AAC" or ".M4A" => CreateAacEncoder(filePath, format),
                ".FLAC" => throw new NotSupportedException("FLAC encoding is not supported in this version."),
                _ => new WaveWriter(filePath, format)
            };
        }
        catch (Exception ex) when (ex is not NotSupportedException)
        {
            throw new AudioRecorderException($"Failed to create output file: {filePath}", ex);
        }
    }

    private static MediaFoundationEncoder CreateMp3Encoder(string filePath, WaveFormat format)
    {
        try
        {
            var encoder = MediaFoundationEncoder.CreateMP3Encoder(format, filePath, 192000);
            return encoder;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MP3 encoder creation failed: {ex.Message}");
            throw new NotSupportedException("MP3 encoding is not supported on this system.", ex);
        }
    }

    private static MediaFoundationEncoder CreateAacEncoder(string filePath, WaveFormat format)
    {
        try
        {
            var encoder = MediaFoundationEncoder.CreateAACEncoder(format, filePath, 192000);
            return encoder;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AAC encoder creation failed: {ex.Message}");
            throw new NotSupportedException("AAC encoding is not supported on this system.", ex);
        }
    }

    private void WriteToOutput(byte[] buffer, int offset, int count)
    {
        if (_writerDisposable is IWriteable writeable)
        {
            writeable.Write(buffer, offset, count);
        }
    }

    private void ResetMetrics()
    {
        _bytesWritten = 0;
        lock (_levelLock)
        {
            _peakLevel = 0;
            _averageLevel = 0;
        }
    }

    private static IWaveSource BuildProcessingChain(SoundInSource source, IAudioFilter? filter, AudioSettings? settings)
    {
        IWaveSource processedSource = source;

        if (filter != null)
        {
            try
            {
                var filterCompatibleSource = processedSource
                    .ToSampleSource()
                    .ToWaveSource(16)
                    .ToStereo();

                processedSource = filter.ApplyFilter(filterCompatibleSource);
                Debug.WriteLine($"Filter applied: {filter.GetType().Name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to apply filter: {ex.Message}. Continuing without filter.");
            }
        }

        if (settings != null && settings.IsValid())
        {
            try
            {
                processedSource = processedSource.ChangeSampleRate(settings.SampleRate);
                processedSource = processedSource.ToSampleSource().ToWaveSource(settings.BitsPerSample);
                processedSource = settings.Channels == 1
                    ? processedSource.ToMono()
                    : processedSource.ToStereo();

                Debug.WriteLine($"Applied settings: {settings.SampleRate}Hz, {settings.BitsPerSample}bit, {settings.Channels}ch");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to apply settings: {ex.Message}");
                throw new AudioRecorderException($"Cannot apply audio settings: {ex.Message}", ex);
            }
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
                    if (_isWriting && _writerDisposable != null)
                    {
                        WriteToOutput(_readBuffer, 0, bytesRead);
                        Interlocked.Add(ref _bytesWritten, bytesRead);
                    }
                    else
                    {
                        break;
                    }
                }

                ProcessAudioData(_readBuffer, bytesRead);
            }
        }
        catch (ObjectDisposedException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in OnDataAvailable: {ex.Message}");
        }
    }

    private void ProcessAudioData(byte[] buffer, int bytesRead)
    {
        if (_processedSource == null || bytesRead == 0)
        {
            return;
        }

        try
        {
            float[] samples = ConvertBytesToFloatSamples(buffer, bytesRead, _processedSource.WaveFormat);

            if (samples.Length > 0)
            {
                CalculateAudioLevels(samples);
                AudioDataAvailable?.Invoke(this, new AudioDataEventArgs(samples));
                NotifyRecordingProgress();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error processing audio data: {ex.Message}");
        }
    }

    private void CalculateAudioLevels(float[] samples)
    {
        if (samples.Length == 0)
            return;

        float peak = 0f;
        float sum = 0f;

        for (int i = 0; i < samples.Length; i++)
        {
            float abs = Math.Abs(samples[i]);
            if (abs > peak)
                peak = abs;
            sum += abs;
        }

        float average = sum / samples.Length;

        lock (_levelLock)
        {
            _peakLevel = Math.Max(peak, _peakLevel * 0.95f);
            _averageLevel = (_averageLevel * 0.9f) + (average * 0.1f);
        }

        AudioLevelChanged?.Invoke(this, new AudioLevelEventArgs(_peakLevel, _averageLevel));
    }

    private void NotifyRecordingProgress()
    {
        RecordingProgressChanged?.Invoke(this, new RecordingProgressEventArgs(
            RecordingDuration,
            BytesRecorded,
            PeakLevel,
            AverageLevel));
    }

    private static float[] ConvertBytesToFloatSamples(byte[] buffer, int count, WaveFormat format)
    {
        int bytesPerSample = format.BitsPerSample / 8;

        if (bytesPerSample == 0)
            return [];

        int sampleCount = count / bytesPerSample;

        if (sampleCount == 0)
            return [];

        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            int byteIndex = i * bytesPerSample;

            switch (format.BitsPerSample)
            {
                case 8 when byteIndex < count:
                    samples[i] = (buffer[byteIndex] - 128) / 128f;
                    break;

                case 16 when byteIndex + 1 < count:
                    short sample16 = (short)(buffer[byteIndex] | (buffer[byteIndex + 1] << 8));
                    samples[i] = sample16 / 32768f;
                    break;

                case 24 when byteIndex + 2 < count:
                    int sample24 = buffer[byteIndex]
                                   | (buffer[byteIndex + 1] << 8)
                                   | (buffer[byteIndex + 2] << 16);
                    if ((sample24 & 0x800000) != 0)
                        sample24 |= unchecked((int)0xFF000000);
                    samples[i] = sample24 / 8388608f;
                    break;

                case 32 when format.WaveFormatTag == AudioEncoding.IeeeFloat && byteIndex + 3 < count:
                    samples[i] = BitConverter.ToSingle(buffer, byteIndex);
                    break;

                case 32 when byteIndex + 3 < count:
                    int sample32 = BitConverter.ToInt32(buffer, byteIndex);
                    samples[i] = sample32 / 2147483648f;
                    break;

                default:
                    samples[i] = 0f;
                    break;
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
                return;

            _isWriting = false;
            IsRecording = false;
            _recordingStopwatch.Stop();

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

            DisposeWriter();
            CleanupRecordingResources();

            Debug.WriteLine($"Recording stopped. Duration: {RecordingDuration}, Bytes: {BytesRecorded}");
        }
        finally
        {
            _recordingLock.Release();
        }
    }

    public async Task PauseRecordingAsync(CancellationToken cancellationToken = default)
    {
        await _recordingLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsRecording || !_isWriting)
                return;

            _isWriting = false;
            _recordingStopwatch.Stop();
            Debug.WriteLine("Recording paused");
        }
        finally
        {
            _recordingLock.Release();
        }
    }

    public async Task ResumeRecordingAsync(CancellationToken cancellationToken = default)
    {
        await _recordingLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsRecording || _isWriting)
                return;

            _isWriting = true;
            _recordingStopwatch.Start();
            Debug.WriteLine("Recording resumed");
        }
        finally
        {
            _recordingLock.Release();
        }
    }

    private void DisposeWriter()
    {
        lock (_writerLock)
        {
            try
            {
                _writerDisposable?.Dispose();
                Debug.WriteLine("Writer disposed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error disposing writer: {ex.Message}");
            }

            _writerDisposable = null;
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

        DisposeWriter();
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
                $"Access to microphone '{deviceName}' was denied. Check Windows Privacy Settings."),

            _ => (
                AudioDeviceErrorType.InitializationFailed,
                $"Failed to initialize microphone '{deviceName}'. Error: {ex.Message}")
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        CleanupRecordingResources();
        _recordingLock.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
