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
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed in CleanupCaptureResources method")]
    private WasapiCapture? _capture;

    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed in CleanupResources method")]
    private WaveWriter? _writer;

    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed in CleanupCaptureResources method")]
    private SoundInSource? _soundInSource;

    private IWaveSource? _filteredSource;
    private bool _disposed;
    private bool _isFirstDataReceived;

    public IWaveSource? CaptureSource => _soundInSource;

    public event EventHandler? RecordingStarted;

    public void StartRecording(string outputFilePath, MMDevice device, IAudioFilter? filter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFilePath);
        ArgumentNullException.ThrowIfNull(device);

        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_capture != null)
        {
            throw new InvalidOperationException("Recording is already in progress.");
        }

        try
        {
            _isFirstDataReceived = false;

            _capture = new WasapiCapture(true, AudioClientShareMode.Shared, 100);
            _capture.Device = device;
            _capture.Initialize();

            _soundInSource = new SoundInSource(_capture) { FillWithZeros = false };

            _filteredSource = filter != null
                ? filter.ApplyFilter(_soundInSource)
                : _soundInSource;

            _writer = new WaveWriter(outputFilePath, _filteredSource.WaveFormat);

            byte[] buffer = new byte[_filteredSource.WaveFormat.BytesPerSecond / 2];

            _capture.DataAvailable += (s, e) =>
            {
                try
                {
                    int read;
                    while ((read = _filteredSource.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        _writer?.Write(buffer, 0, read);

                        if (!_isFirstDataReceived)
                        {
                            _isFirstDataReceived = true;
                            RecordingStarted?.Invoke(this, EventArgs.Empty);
                        }
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
        catch (CoreAudioAPIException ex) when (ex.ErrorCode == unchecked((int)0x80070005))
        {
            CleanupResources();
            throw new UnauthorizedAccessException(
                "Microphone access is denied. Please check your privacy settings.", ex);
        }
        catch (CoreAudioAPIException ex)
        {
            CleanupResources();
            throw new AudioRecorderException("Failed to initialize audio capture device", ex);
        }
        catch (IOException ex)
        {
            CleanupResources();
            throw new AudioRecorderException($"Failed to create output file: {outputFilePath}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            CleanupResources();
            throw new AudioRecorderException("Access denied to output file or microphone", ex);
        }
        catch (ArgumentException ex)
        {
            CleanupResources();
            throw new AudioRecorderException("Invalid argument provided", ex);
        }
        catch (InvalidOperationException ex)
        {
            CleanupResources();
            throw new AudioRecorderException("Invalid operation during recording initialization", ex);
        }
    }

    public void StopRecording()
    {
        if (_capture == null)
        {
            return;
        }

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

        try
        {
            if (_writer != null)
            {
                _writer.Dispose();
            }
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"IO error disposing writer: {ex.Message}");
        }
        catch (ObjectDisposedException ex)
        {
            Debug.WriteLine($"Writer already disposed: {ex.Message}");
        }
        finally
        {
            _writer = null;
        }

        CleanupCaptureResources();
    }

    private void CleanupCaptureResources()
    {
        if (_capture != null)
        {
            try
            {
                _capture.Dispose();
            }
            catch (ObjectDisposedException ex)
            {
                Debug.WriteLine($"Capture already disposed: {ex.Message}");
            }
            catch (CoreAudioAPIException ex)
            {
                Debug.WriteLine($"Audio API error disposing capture: {ex.Message}");
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
            catch (ObjectDisposedException ex)
            {
                Debug.WriteLine($"Sound source already disposed: {ex.Message}");
            }
            finally
            {
                _soundInSource = null;
            }
        }

        _filteredSource = null;
    }

    private void CleanupResources()
    {
        CleanupCaptureResources();

        if (_writer != null)
        {
            try
            {
                _writer.Dispose();
            }
            catch (ObjectDisposedException ex)
            {
                Debug.WriteLine($"Writer already disposed: {ex.Message}");
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"IO error disposing writer: {ex.Message}");
            }
            finally
            {
                _writer = null;
            }
        }
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
            catch (CoreAudioAPIException ex)
            {
                Debug.WriteLine($"Audio API error stopping capture: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine($"Invalid operation stopping capture: {ex.Message}");
            }
        }

        _soundInSource = newSource as SoundInSource;

        if (_soundInSource != null && _capture != null)
        {
            try
            {
                _capture.Start();
            }
            catch (CoreAudioAPIException ex)
            {
                Debug.WriteLine($"Audio API error restarting capture: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine($"Invalid operation restarting capture: {ex.Message}");
            }
        }
        else if (_soundInSource == null)
        {
            Debug.WriteLine("newSource is not a SoundInSource");
        }
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                StopRecording();
                CleanupResources();
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
