using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CSCore;
using CSCore.Codecs.WAV;
using CSCore.CoreAudioAPI;
using CSCore.SoundIn;
using CSCore.Streams;
using VoiceRecorder.Exceptions;
using VoiceRecorder.Filters.Interfaces;
using VoiceRecorder.Interfaces;

namespace VoiceRecorder.Services;

internal sealed class AudioRecorder : IAudioRecorder
{
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed")]
    private WasapiCapture? _capture;

    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed")]
    private WaveWriter? _writer;

    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed")]
    private SoundInSource? _soundInSource;

    private IWaveSource? _filteredSource;
    private bool _disposed;
    private readonly SemaphoreSlim _recordingLock = new(1, 1);
    private CancellationTokenSource? _recordingCts;

    public IWaveSource? CaptureSource => _soundInSource;
    public bool IsRecording { get; private set; }

    public event EventHandler? RecordingStarted;

    public async Task StartRecordingAsync(string outputFilePath, MMDevice device, IAudioFilter? filter,
        CancellationToken cancellationToken = default)
    {
        await _recordingLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsRecording)
            {
                throw new InvalidOperationException("Recording is already in progress");
            }

            _recordingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            await Task.Run(() => InitializeRecording(outputFilePath, device, filter, _recordingCts.Token),
                    _recordingCts.Token)
                .ConfigureAwait(false);

            IsRecording = true;
            RecordingStarted?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            await CleanupResourcesAsync().ConfigureAwait(false);
            throw;
        }
        catch (CoreAudioAPIException ex) when (ex.ErrorCode == unchecked((int)0x80070005))
        {
            await CleanupResourcesAsync().ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                "Microphone access is denied. Please check your privacy settings.", ex);
        }
        catch (CoreAudioAPIException ex)
        {
            await CleanupResourcesAsync().ConfigureAwait(false);
            throw new AudioRecorderException("Failed to initialize audio capture device", ex);
        }
        catch (IOException ex)
        {
            await CleanupResourcesAsync().ConfigureAwait(false);
            throw new AudioRecorderException($"Failed to create output file: {outputFilePath}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            await CleanupResourcesAsync().ConfigureAwait(false);
            throw new AudioRecorderException("Access denied to output file or microphone", ex);
        }
        catch (Exception ex)
        {
            await CleanupResourcesAsync().ConfigureAwait(false);
            throw new AudioRecorderException("Failed to start recording", ex);
        }
        finally
        {
            _recordingLock.Release();
        }
    }

    private void InitializeRecording(string outputFilePath, MMDevice device, IAudioFilter? filter,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _capture = new WasapiCapture(true, AudioClientShareMode.Shared, 100)
        {
            Device = device
        };
        _capture.Initialize();

        cancellationToken.ThrowIfCancellationRequested();

        _soundInSource = new SoundInSource(_capture) { FillWithZeros = false };

        _filteredSource = filter != null
            ? filter.ApplyFilter(_soundInSource)
            : _soundInSource;

        _writer = new WaveWriter(outputFilePath, _filteredSource.WaveFormat);

        byte[] buffer = new byte[_filteredSource.WaveFormat.BytesPerSecond / 2];

        _capture.DataAvailable += (s, e) =>
        {
            if (cancellationToken.IsCancellationRequested) return;

            try
            {
                int read;
                while ((read = _filteredSource.Read(buffer, 0, buffer.Length)) > 0)
                {
                    _writer?.Write(buffer, 0, read);
                }
            }
            catch (ObjectDisposedException ex)
            {
                Debug.WriteLine($"Object disposed in DataAvailable: {ex.Message}");
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"IO error in DataAvailable: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine($"Invalid operation in DataAvailable: {ex.Message}");
            }
        };

        _capture.Start();
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

            _recordingCts?.CancelAsync();

            await Task.Run(() =>
            {
                if (_capture != null)
                {
                    try
                    {
                        _capture.Stop();
                    }
                    catch (CoreAudioAPIException ex)
                    {
                        Debug.WriteLine($"Audio API error stopping capture: {ex.Message}");
                    }
                    catch (InvalidOperationException ex)
                    {
                        Debug.WriteLine($"Invalid operation stopping capture: {ex.Message}");
                    }
                }

                if (_writer != null)
                {
                    try
                    {
                        _writer.Dispose();
                        _writer = null;
                    }
                    catch (IOException ex)
                    {
                        Debug.WriteLine($"IO error disposing writer: {ex.Message}");
                    }
                    catch (ObjectDisposedException ex)
                    {
                        Debug.WriteLine($"Writer already disposed: {ex.Message}");
                    }
                }
            }, cancellationToken).ConfigureAwait(false);

            IsRecording = false;
        }
        finally
        {
            _recordingLock.Release();
        }
    }

    private async Task CleanupResourcesAsync()
    {
        await Task.Run(() =>
        {
            if (_capture != null)
            {
                try
                {
                    _capture.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error disposing capture: {ex.Message}");
                }
                finally
                {
                    _capture = null;
                }
            }

            if (_soundInSource != null)
            {
                try
                {
                    _soundInSource.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error disposing sound source: {ex.Message}");
                }
                finally
                {
                    _soundInSource = null;
                }
            }

            if (_writer != null)
            {
                try
                {
                    _writer.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error disposing writer: {ex.Message}");
                }
                finally
                {
                    _writer = null;
                }
            }

            _filteredSource = null;
        }).ConfigureAwait(false);
    }

    public void UpdateSource(IWaveSource newSource)
    {
        ArgumentNullException.ThrowIfNull(newSource);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_capture != null)
        {
            try
            {
                _capture.Stop();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping capture: {ex.Message}");
            }
        }

        _soundInSource = newSource as SoundInSource;

        if (_soundInSource != null && _capture != null)
        {
            try
            {
                _capture.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error restarting capture: {ex.Message}");
            }
        }
    }

    private async void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                await StopRecordingAsync().ConfigureAwait(false);
                await CleanupResourcesAsync().ConfigureAwait(false);
                _recordingCts?.Dispose();
                _recordingLock.Dispose();
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
