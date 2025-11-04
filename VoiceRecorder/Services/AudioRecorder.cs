using System.Diagnostics;
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
    private WasapiCapture? _capture;
    private WaveWriter? _writer;
    private bool _disposed;
    private SoundInSource? _soundInSource;
    private readonly object _lock = new();
    private bool _isRecording;

    public IWaveSource? CaptureSource
    {
        get
        {
            lock (_lock)
            {
                return _soundInSource;
            }
        }
    }

    public void StartRecording(string outputFilePath, MMDevice device, IAudioFilter? filter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFilePath);
        ArgumentNullException.ThrowIfNull(device);

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_isRecording)
            {
                throw new InvalidOperationException("Recording is already in progress.");
            }

            try
            {
                _capture = new WasapiCapture(true, AudioClientShareMode.Shared, 100);
                _capture.Device = device;
                _capture.Initialize();

                _soundInSource = new SoundInSource(_capture) { FillWithZeros = false };

                IWaveSource filteredSource;
                if (filter != null)
                {
                    filteredSource = filter.ApplyFilter(_soundInSource);
                }
                else
                {
                    filteredSource = _soundInSource;
                }

                _writer = new WaveWriter(outputFilePath, filteredSource.WaveFormat);

                byte[] buffer = new byte[filteredSource.WaveFormat.BytesPerSecond / 2];

                _capture.DataAvailable += (s, e) =>
                {
                    int read;
                    while ((read = filteredSource.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        _writer.Write(buffer, 0, read);
                    }
                };

                _capture.Start();
                _isRecording = true;
            }
            catch (CoreAudioAPIException ex) when (ex.ErrorCode == unchecked((int)0x80070005))
            {
                CleanupResources();
                throw new UnauthorizedAccessException(
                    "Microphone access is denied. Please check your privacy settings.", ex);
            }
            catch (CoreAudioAPIException coreEx)
            {
                CleanupResources();
                throw new AudioRecorderException("Failed to initialize audio capture device", coreEx);
            }
            catch (IOException ioEx)
            {
                CleanupResources();
                throw new AudioRecorderException($"Failed to create output file: {outputFilePath}", ioEx);
            }
            catch (UnauthorizedAccessException uaEx)
            {
                CleanupResources();
                throw new AudioRecorderException("Access denied to output file or microphone", uaEx);
            }
            catch (ArgumentException argEx)
            {
                CleanupResources();
                throw new AudioRecorderException("Invalid argument provided", argEx);
            }
            catch (InvalidOperationException invEx)
            {
                CleanupResources();
                throw new AudioRecorderException("Invalid operation during recording initialization", invEx);
            }
        }
    }

    public void StopRecording()
    {
        lock (_lock)
        {
            if (!_isRecording)
            {
                return;
            }

            _isRecording = false;

            if (_capture != null)
            {
                try
                {
                    _capture.Stop();
                }
                catch (CoreAudioAPIException ex)
                {
                    Debug.WriteLine($"Error stopping capture: {ex.Message}");
                    throw new AudioRecorderException("Failed to stop capture", ex);
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
                    Debug.WriteLine($"Error disposing writer: {ex.Message}");
                    throw new AudioRecorderException("Failed to finalize recording", ex);
                }
            }
        }
    }

    public void UpdateSource(IWaveSource newSource)
    {
        ArgumentNullException.ThrowIfNull(newSource);

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _capture?.Stop();

            _soundInSource = newSource as SoundInSource;

            if (_soundInSource != null && _isRecording)
            {
                _capture?.Start();
            }
            else if (_soundInSource == null)
            {
                Debug.WriteLine("newSource is not a SoundInSource");
            }
        }
    }

    private void CleanupResources()
    {
        try
        {
            _soundInSource?.Dispose();
        }
        catch (ObjectDisposedException odEx)
        {
            Debug.WriteLine($"SoundInSource already disposed: {odEx.Message}");
        }
        finally
        {
            _soundInSource = null;
        }

        try
        {
            _capture?.Dispose();
        }
        catch (ObjectDisposedException odEx)
        {
            Debug.WriteLine($"Capture already disposed: {odEx.Message}");
        }
        finally
        {
            _capture = null;
        }

        try
        {
            _writer?.Dispose();
        }
        catch (ObjectDisposedException odEx)
        {
            Debug.WriteLine($"Writer already disposed: {odEx.Message}");
        }
        catch (IOException ioEx)
        {
            Debug.WriteLine($"Error disposing writer: {ioEx.Message}");
        }
        finally
        {
            _writer = null;
        }

        _isRecording = false;
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                lock (_lock)
                {
                    if (_isRecording)
                    {
                        try
                        {
                            StopRecording();
                        }
                        catch (AudioRecorderException arEx)
                        {
                            Debug.WriteLine($"Error stopping recording during dispose: {arEx.Message}");
                        }
                        catch (CoreAudioAPIException coreEx)
                        {
                            Debug.WriteLine($"Audio API error during dispose: {coreEx.Message}");
                        }
                    }

                    CleanupResources();
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
