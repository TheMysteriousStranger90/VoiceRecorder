using System.Diagnostics;
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
    private readonly object _stateLock = new();
    private bool _disposed;
    private volatile bool _isWriting;
    private volatile bool _isInitialized;
    private byte[]? _readBuffer;
    private long _bytesWritten;
    private readonly Stopwatch _recordingStopwatch = new();
    private readonly ManualResetEventSlim _captureReadyEvent = new(false);

    private float _peakLevel;
    private float _averageLevel;
    private readonly object _levelLock = new();

    private const int PreRollBufferMs = 500;
    private byte[]? _preRollBuffer;
    private int _preRollBytesWritten;
    private volatile bool _isPreRolling;

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

            _captureReadyEvent.Reset();
            _isInitialized = false;
            _isPreRolling = true;

            _capture = new WasapiCapture(false, AudioClientShareMode.Shared, 50)
            {
                Device = _currentDevice
            };
            _capture.Initialize();

            Debug.WriteLine($"Capture initialized: {_currentDevice.FriendlyName}, Format: {_capture.WaveFormat}");

            _soundInSource = new SoundInSource(_capture) { FillWithZeros = false };

            OutputFormat outputFormat = GetOutputFormatFromPath(outputFilePath);

            _processedSource = BuildProcessingChain(_soundInSource, filter, settings, outputFormat);

            int bufferSize = CalculateOptimalBufferSize(_processedSource.WaveFormat);
            _readBuffer = new byte[bufferSize];

            int preRollBufferSize = (_processedSource.WaveFormat.BytesPerSecond * PreRollBufferMs) / 1000;
            _preRollBuffer = new byte[preRollBufferSize];
            _preRollBytesWritten = 0;

            _writerDisposable = CreateWriter(outputFilePath, _processedSource.WaveFormat, outputFormat) as IDisposable;

            if (filter != null)
            {
                PrimeFilterChain(_processedSource, bufferSize);
            }

            _soundInSource.DataAvailable += OnDataAvailable;

            lock (_stateLock)
            {
                _isWriting = true;
                IsRecording = true;
                _bytesWritten = 0;
            }

            _capture.Start();

            await WaitForCaptureReadyAsync(cancellationToken).ConfigureAwait(false);

            _recordingStopwatch.Restart();

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

    private static OutputFormat GetOutputFormatFromPath(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToUpperInvariant();
        return extension switch
        {
            ".MP3" => OutputFormat.Mp3,
            ".AAC" or ".M4A" => OutputFormat.Aac,
            ".WAV" => OutputFormat.Wav,
            _ => OutputFormat.Wav
        };
    }

    private async Task WaitForCaptureReadyAsync(CancellationToken cancellationToken)
    {
        const int maxWaitMs = 2000;
        const int checkIntervalMs = 10;
        int waited = 0;

        while (!_isInitialized && waited < maxWaitMs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(checkIntervalMs, cancellationToken).ConfigureAwait(false);
            waited += checkIntervalMs;
        }

        if (!_isInitialized)
        {
            Debug.WriteLine("Warning: Capture ready timeout, proceeding anyway");
        }
        else
        {
            Debug.WriteLine($"Capture ready after {waited}ms");
        }

        _isPreRolling = false;

        FlushPreRollBuffer();
    }

    private void FlushPreRollBuffer()
    {
        if (_preRollBuffer == null || _preRollBytesWritten == 0)
        {
            return;
        }

        lock (_writerLock)
        {
            if (_isWriting && _writerDisposable != null)
            {
                try
                {
                    WriteToOutput(_preRollBuffer, 0, _preRollBytesWritten);
                    Interlocked.Add(ref _bytesWritten, _preRollBytesWritten);
                    Debug.WriteLine($"Flushed {_preRollBytesWritten} bytes from pre-roll buffer");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error flushing pre-roll buffer: {ex.Message}");
                }
            }
        }

        _preRollBytesWritten = 0;
    }

    private static void PrimeFilterChain(IWaveSource processedSource, int bufferSize)
    {
        try
        {
            byte[] primeBuffer = new byte[bufferSize];
            int primeSamples = 0;
            int maxPrimeSamples = 3;

            while (primeSamples < maxPrimeSamples)
            {
                int read = processedSource.Read(primeBuffer, 0, primeBuffer.Length);
                if (read <= 0)
                    break;
                primeSamples++;
            }

            Debug.WriteLine($"Filter chain primed with {primeSamples} buffer reads");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error priming filter chain: {ex.Message}");
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

    private static IWriteable CreateWriter(string filePath, WaveFormat format, OutputFormat outputFormat)
    {
        try
        {
            return outputFormat switch
            {
                OutputFormat.Wav => new WaveWriter(filePath, format),
                OutputFormat.Mp3 => CreateMp3Encoder(filePath, format),
                OutputFormat.Aac => CreateAacEncoder(filePath, format),
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
            var supportedFormats = MediaFoundationEncoder.GetEncoderMediaTypes(AudioSubTypes.MpegLayer3);

            if (supportedFormats.Length == 0)
            {
                throw new NotSupportedException(
                    "MP3 encoding is not supported on this system. " +
                    "Please install Windows Media Feature Pack or use WAV format.");
            }

            bool isFormatSupported = supportedFormats.Any(f =>
                f.SampleRate == format.SampleRate &&
                f.Channels == format.Channels);

            if (!isFormatSupported)
            {
                var supportedList = string.Join(", ",
                    supportedFormats.Select(f => $"{f.SampleRate}Hz/{f.Channels}ch").Distinct());

                throw new NotSupportedException(
                    $"MP3 encoder does not support {format.SampleRate}Hz with {format.Channels} channels. " +
                    $"Supported combinations: {supportedList}. " +
                    $"This should have been converted automatically - please report this issue.");
            }

            var encoder = MediaFoundationEncoder.CreateMP3Encoder(format, filePath, 192000);
            Debug.WriteLine(
                $"MP3 encoder created successfully: {format.SampleRate}Hz, {format.Channels}ch, {format.BitsPerSample}bit");
            return encoder;
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MP3 encoder creation failed: {ex.Message}");
            throw new NotSupportedException(
                "Failed to create MP3 encoder. Your system may not support MP3 encoding. " +
                "Please use WAV format instead.", ex);
        }
    }

    private static MediaFoundationEncoder CreateAacEncoder(string filePath, WaveFormat format)
    {
        try
        {
            var supportedFormats = MediaFoundationEncoder.GetEncoderMediaTypes(AudioSubTypes.MPEG_ADTS_AAC);

            if (supportedFormats.Length == 0)
            {
                throw new NotSupportedException(
                    "AAC encoding is not supported on this system. " +
                    "Please install Windows Media Feature Pack or use WAV format.");
            }

            bool isFormatSupported = supportedFormats.Any(f =>
                f.SampleRate == format.SampleRate &&
                f.Channels == format.Channels);

            if (!isFormatSupported)
            {
                var supportedList = string.Join(", ",
                    supportedFormats.Select(f => $"{f.SampleRate}Hz/{f.Channels}ch").Distinct());

                throw new NotSupportedException(
                    $"AAC encoder does not support {format.SampleRate}Hz with {format.Channels} channels. " +
                    $"Supported combinations: {supportedList}. " +
                    $"This should have been converted automatically - please report this issue.");
            }

            var encoder = MediaFoundationEncoder.CreateAACEncoder(format, filePath, 192000);
            Debug.WriteLine(
                $"AAC encoder created successfully: {format.SampleRate}Hz, {format.Channels}ch, {format.BitsPerSample}bit");
            return encoder;
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AAC encoder creation failed: {ex.Message}");
            throw new NotSupportedException(
                "Failed to create AAC encoder. Your system may not support AAC encoding. " +
                "Please use WAV format instead.", ex);
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
        _recordingStopwatch.Reset();
        lock (_levelLock)
        {
            _peakLevel = 0;
            _averageLevel = 0;
        }
    }

    private static IWaveSource BuildProcessingChain(
        SoundInSource source,
        IAudioFilter? filter,
        AudioSettings? settings,
        OutputFormat outputFormat)
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

                Debug.WriteLine(
                    $"Applied settings: {settings.SampleRate}Hz, {settings.BitsPerSample}bit, {settings.Channels}ch");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to apply settings: {ex.Message}");
                throw new AudioRecorderException($"Cannot apply audio settings: {ex.Message}", ex);
            }
        }

        if (outputFormat == OutputFormat.Mp3 || outputFormat == OutputFormat.Aac)
        {
            processedSource = PrepareSourceForCompressedEncoder(processedSource, outputFormat);
        }

        return processedSource;
    }

    private static IWaveSource PrepareSourceForCompressedEncoder(IWaveSource source, OutputFormat outputFormat)
    {
        var audioSubType = outputFormat == OutputFormat.Mp3
            ? AudioSubTypes.MpegLayer3
            : AudioSubTypes.MPEG_ADTS_AAC;

        try
        {
            var supportedFormats = MediaFoundationEncoder.GetEncoderMediaTypes(audioSubType);

            if (supportedFormats.Length == 0)
            {
                throw new NotSupportedException(
                    $"{outputFormat} encoding is not supported on this system.");
            }

            var currentFormat = source.WaveFormat;
            Debug.WriteLine(
                $"Current format before encoder preparation: {currentFormat.SampleRate}Hz, {currentFormat.Channels}ch, {currentFormat.BitsPerSample}bit");

            bool isFormatSupported = supportedFormats.Any(f =>
                f.SampleRate == currentFormat.SampleRate &&
                f.Channels == currentFormat.Channels);

            if (isFormatSupported && currentFormat.BitsPerSample == 16)
            {
                Debug.WriteLine("Format is already compatible with encoder");
                return source;
            }

            var targetFormat = supportedFormats
                .Where(f => f.Channels == currentFormat.Channels)
                .OrderBy(f => Math.Abs(f.SampleRate - currentFormat.SampleRate))
                .FirstOrDefault();

            if (targetFormat == null)
            {
                targetFormat = supportedFormats
                    .OrderBy(f => Math.Abs(f.SampleRate - currentFormat.SampleRate))
                    .First();

                Debug.WriteLine($"Converting channels: {currentFormat.Channels} -> {targetFormat.Channels}");
                source = targetFormat.Channels == 1 ? source.ToMono() : source.ToStereo();
            }

            if (currentFormat.SampleRate != targetFormat.SampleRate)
            {
                Debug.WriteLine($"Converting sample rate: {currentFormat.SampleRate}Hz -> {targetFormat.SampleRate}Hz");
                source = source.ChangeSampleRate(targetFormat.SampleRate);
            }

            if (currentFormat.BitsPerSample != 16)
            {
                Debug.WriteLine($"Converting bit depth: {currentFormat.BitsPerSample}bit -> 16bit");
                source = source.ToSampleSource().ToWaveSource(16);
            }

            Debug.WriteLine(
                $"Format prepared for {outputFormat} encoder: {source.WaveFormat.SampleRate}Hz, {source.WaveFormat.Channels}ch, {source.WaveFormat.BitsPerSample}bit");
            return source;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error preparing source for encoder: {ex.Message}");
            throw new AudioRecorderException(
                $"Failed to prepare audio format for {outputFormat} encoding. " +
                $"Your system may not support this format.", ex);
        }
    }

    private void OnDataAvailable(object? sender, DataAvailableEventArgs e)
    {
        bool shouldWrite;
        lock (_stateLock)
        {
            shouldWrite = _isWriting;
        }

        if (!shouldWrite || _processedSource == null || _readBuffer == null)
        {
            return;
        }

        if (!_isInitialized)
        {
            _isInitialized = true;
            _captureReadyEvent.Set();
        }

        try
        {
            int bytesRead;

            while (shouldWrite && (bytesRead = _processedSource.Read(_readBuffer, 0, _readBuffer.Length)) > 0)
            {
                lock (_stateLock)
                {
                    shouldWrite = _isWriting;
                }

                if (!shouldWrite)
                    break;

                if (_isPreRolling && _preRollBuffer != null)
                {
                    int availableSpace = _preRollBuffer.Length - _preRollBytesWritten;
                    int bytesToCopy = Math.Min(bytesRead, availableSpace);

                    if (bytesToCopy > 0)
                    {
                        Buffer.BlockCopy(_readBuffer, 0, _preRollBuffer, _preRollBytesWritten, bytesToCopy);
                        _preRollBytesWritten += bytesToCopy;
                    }
                }
                else
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

                case 32 when (format.WaveFormatTag == AudioEncoding.IeeeFloat ||
                              format.WaveFormatTag == AudioEncoding.Extensible) && byteIndex + 3 < count:
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

            lock (_stateLock)
            {
                _isWriting = false;
                IsRecording = false;
            }

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

            lock (_stateLock)
            {
                _isWriting = false;
            }

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

            lock (_stateLock)
            {
                _isWriting = true;
            }

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
        lock (_stateLock)
        {
            _isWriting = false;
            _isInitialized = false;
            _isPreRolling = false;
        }

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
        _preRollBuffer = null;
        _preRollBytesWritten = 0;

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
        _captureReadyEvent.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
