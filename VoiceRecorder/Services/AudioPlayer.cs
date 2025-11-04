using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CSCore;
using CSCore.Codecs;
using CSCore.SoundOut;
using VoiceRecorder.Interfaces;
using VoiceRecorder.Models;
using PlaybackState = VoiceRecorder.Models.PlaybackState;

namespace VoiceRecorder.Services;

internal sealed class AudioPlayer : IAudioPlayer
{
    private IWaveSource? _waveSource;

    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance",
        Justification = "ISoundOut provides flexibility for different sound output implementations")]
    private ISoundOut? _soundOut;

    private bool _disposed;
    private float _volume = 1.0f;
    private PlaybackState _playbackState = PlaybackState.Stopped;
    private string? _currentFilePath;

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            if (_soundOut != null)
            {
                _soundOut.Volume = _volume;
            }
        }
    }

    public PlaybackState CurrentPlaybackState => _playbackState;

    public event EventHandler<PlaybackStatusEventArgs>? PlaybackStatusChanged;

    public void PlayFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        StopFile();

        try
        {
            _currentFilePath = filePath;
            _waveSource = CodecFactory.Instance.GetCodec(_currentFilePath);
            _soundOut = new WasapiOut();
            _soundOut.Stopped += OnPlaybackStopped;
            _soundOut.Initialize(_waveSource);
            _soundOut.Volume = _volume;
            _soundOut.Play();
            _playbackState = PlaybackState.Playing;

            PlaybackStatusChanged?.Invoke(this, new PlaybackStatusEventArgs(
                PlaybackState.Playing,
                Path.GetFileName(_currentFilePath)));
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"IO error during playback: {ex.Message}");
            _currentFilePath = null;
            _playbackState = PlaybackState.Stopped;
            PlaybackStatusChanged?.Invoke(this, new PlaybackStatusEventArgs(
                $"File access error: {ex.Message}",
                Path.GetFileName(filePath)));
        }
        catch (NotSupportedException ex)
        {
            Debug.WriteLine($"Unsupported format: {ex.Message}");
            _currentFilePath = null;
            _playbackState = PlaybackState.Stopped;
            PlaybackStatusChanged?.Invoke(this, new PlaybackStatusEventArgs(
                $"Unsupported audio format: {ex.Message}",
                Path.GetFileName(filePath)));
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"Access denied: {ex.Message}");
            _currentFilePath = null;
            _playbackState = PlaybackState.Stopped;
            PlaybackStatusChanged?.Invoke(this, new PlaybackStatusEventArgs(
                $"Access denied: {ex.Message}",
                Path.GetFileName(filePath)));
        }
    }

    public void PausePlayback()
    {
        if (_soundOut != null && _playbackState == PlaybackState.Playing)
        {
            try
            {
                _soundOut.Pause();
                _playbackState = PlaybackState.Paused;
                PlaybackStatusChanged?.Invoke(this, new PlaybackStatusEventArgs(
                    PlaybackState.Paused,
                    Path.GetFileName(_currentFilePath ?? string.Empty)));
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine($"Error pausing playback: {ex.Message}");
            }
        }
    }

    public void ResumePlayback()
    {
        if (_soundOut != null && _playbackState == PlaybackState.Paused)
        {
            try
            {
                _soundOut.Play();
                _playbackState = PlaybackState.Playing;
                PlaybackStatusChanged?.Invoke(this, new PlaybackStatusEventArgs(
                    PlaybackState.Playing,
                    Path.GetFileName(_currentFilePath ?? string.Empty)));
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine($"Error resuming playback: {ex.Message}");
            }
        }
    }

    private void OnPlaybackStopped(object? sender, EventArgs e)
    {
        if (_playbackState != PlaybackState.Stopped)
        {
            string? lastFile = _currentFilePath != null ? Path.GetFileName(_currentFilePath) : null;
            StopInternal(lastFile);
        }
    }

    private void StopInternal(string? previousFileOverride = null)
    {
        if (_soundOut != null)
        {
            _soundOut.Stopped -= OnPlaybackStopped;
            _soundOut.Stop();
            _soundOut.Dispose();
            _soundOut = null;
        }

        if (_waveSource != null)
        {
            _waveSource.Dispose();
            _waveSource = null;
        }

        string? fileForEvent = previousFileOverride ??
                               (_currentFilePath != null ? Path.GetFileName(_currentFilePath) : null);
        _currentFilePath = null;

        if (_playbackState != PlaybackState.Stopped)
        {
            _playbackState = PlaybackState.Stopped;
            PlaybackStatusChanged?.Invoke(this, new PlaybackStatusEventArgs(
                PlaybackState.Stopped,
                fileForEvent ?? string.Empty));
        }
    }

    public void StopFile()
    {
        if (_playbackState == PlaybackState.Stopped && _soundOut == null && _waveSource == null)
        {
            return;
        }

        StopInternal();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            StopFile();
            _disposed = true;
        }
    }
}
