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

namespace VoiceRecorder.Services;

internal sealed class AudioRecorder : IAudioRecorder
{
    private WasapiCapture? _capture;
    private WaveWriter? _writer;
    private SoundInSource? _soundInSource;
    private IWaveSource? _filteredSource;

    private readonly SemaphoreSlim _deviceLock = new(1, 1);
    private bool _disposed;

    public IWaveSource? CaptureSource => _soundInSource;
    public bool IsRecording { get; private set; }

    public event EventHandler? RecordingStarted;
    public event EventHandler<AudioDataEventArgs>? AudioDataAvailable;

    public async Task SetDeviceAsync(MMDevice device)
    {
        await _deviceLock.WaitAsync().ConfigureAwait(false);
        try
        {
            CleanupCapture();

            _capture = new WasapiCapture(false, AudioClientShareMode.Shared, 100)
            {
                Device = device
            };
            _capture.Initialize();

            _soundInSource = new SoundInSource(_capture) { FillWithZeros = false };

            _capture.DataAvailable += OnDataAvailable;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error initializing device: {ex.Message}");
            CleanupCapture();
            throw new AudioRecorderException("Failed to initialize device", ex);
        }
        finally
        {
            _deviceLock.Release();
        }
    }

    public async Task StartRecordingAsync(string outputFilePath, IAudioFilter? filter,
        AudioSettings? settings = null, CancellationToken cancellationToken = default)
    {
        await _deviceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsRecording)
                throw new InvalidOperationException("Recording is already in progress");

            if (_capture == null || _soundInSource == null)
                throw new InvalidOperationException("Device not initialized. Call SetDeviceAsync first.");

            var freshSoundInSource = new SoundInSource(_capture) { FillWithZeros = false };

            IWaveSource processedSource = freshSoundInSource;

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

            _filteredSource = filter != null
                ? filter.ApplyFilter(processedSource)
                : processedSource;

            try
            {
                _writer = new WaveWriter(outputFilePath, _filteredSource.WaveFormat);
            }
            catch (IOException ex)
            {
                throw new AudioRecorderException($"Failed to create output file: {outputFilePath}", ex);
            }

            IsRecording = true;
            _capture.Start();

            RecordingStarted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            await StopRecordingInternalAsync().ConfigureAwait(false);
            throw new AudioRecorderException("Failed to start recording", ex);
        }
        finally
        {
            _deviceLock.Release();
        }
    }

    private void OnDataAvailable(object? sender, DataAvailableEventArgs e)
    {
        if (!IsRecording || _writer == null || _filteredSource == null) return;

        try
        {
            byte[] buffer = new byte[e.ByteCount];
            int read;

            while ((read = _filteredSource.Read(buffer, 0, buffer.Length)) > 0)
            {
                _writer.Write(buffer, 0, read);

                if (AudioDataAvailable != null)
                {
                    float[] samples = ConvertBytesToFloatSamples(buffer, read, _filteredSource.WaveFormat);
                    AudioDataAvailable?.Invoke(this, new AudioDataEventArgs(samples));
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Write error in DataAvailable: {ex.Message}");
        }
    }

    private float[] ConvertBytesToFloatSamples(byte[] buffer, int count, WaveFormat format)
    {
        int bytesPerSample = format.BitsPerSample / 8;
        int sampleCount = count / bytesPerSample;
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            int byteIndex = i * bytesPerSample;

            if (format.BitsPerSample == 16)
            {
                if (byteIndex + 1 < count)
                {
                    short sample = (short)(buffer[byteIndex] | (buffer[byteIndex + 1] << 8));
                    samples[i] = sample / 32768f;
                }
            }
            else if (format.BitsPerSample == 32 && format.WaveFormatTag == AudioEncoding.IeeeFloat)
            {
                if (byteIndex + 3 < count)
                {
                    samples[i] = BitConverter.ToSingle(buffer, byteIndex);
                }
            }
            else if (format.BitsPerSample == 32)
            {
                if (byteIndex + 3 < count)
                {
                    int sample = BitConverter.ToInt32(buffer, byteIndex);
                    samples[i] = sample / 2147483648f;
                }
            }
        }

        return samples;
    }

    public async Task StopRecordingAsync(CancellationToken cancellationToken = default)
    {
        await _deviceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopRecordingInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _deviceLock.Release();
        }
    }

    private Task StopRecordingInternalAsync()
    {
        IsRecording = false;

        if (_capture != null)
        {
            try
            {
                _capture.Stop();
            }
            catch
            {
                /* ignore */
            }
        }

        if (_writer != null && !_writer.IsDisposed)
        {
            try
            {
                _writer.Dispose();
            }
            catch
            {
                /* ignore */
            }

            _writer = null;
        }

        _filteredSource?.Dispose();
        _filteredSource = null;
        return Task.CompletedTask;
    }

    private void CleanupCapture()
    {
        if (_capture != null)
        {
            _capture.Stop();
            _capture.DataAvailable -= OnDataAvailable;
            _capture.Dispose();
            _capture = null;
        }

        _soundInSource?.Dispose();
        _soundInSource = null;

        _writer?.Dispose();
        _writer = null;
    }

    public void Dispose()
    {
        if (_disposed) return;

        CleanupCapture();
        _deviceLock.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
