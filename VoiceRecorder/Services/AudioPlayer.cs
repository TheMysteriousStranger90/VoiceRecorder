using System.Diagnostics;
using CSCore;
using CSCore.Codecs;
using CSCore.SoundOut;
using VoiceRecorder.Exceptions;
using VoiceRecorder.Interfaces;
using VoiceRecorder.Models;
using PlaybackState = VoiceRecorder.Models.PlaybackState;

namespace VoiceRecorder.Services;

internal sealed class AudioPlayer : IAudioPlayer
{
    private IWaveSource? _waveSource;
    private WasapiOut? _soundOut;
    private bool _disposed;
    private float _volume = 1.0f;
    private PlaybackState _playbackState = PlaybackState.Stopped;
    private string? _currentFilePath;
    private readonly object _lock = new();

    public float Volume
    {
        get
        {
            lock (_lock)
            {
                return _volume;
            }
        }
        set
        {
            lock (_lock)
            {
                _volume = Math.Clamp(value, 0f, 1f);
                if (_soundOut != null)
                {
                    _soundOut.Volume = _volume;
                }
            }
        }
    }

    public PlaybackState CurrentPlaybackState
    {
        get
        {
            lock (_lock)
            {
                return _playbackState;
            }
        }
    }

    public event EventHandler<PlaybackStatusEventArgs>? PlaybackStatusChanged;

    public void PlayFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            RaisePlaybackStatusChanged(new PlaybackStatusEventArgs(
                "File not found",
                Path.GetFileName(filePath)));
            return;
        }

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

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

                RaisePlaybackStatusChanged(new PlaybackStatusEventArgs(
                    PlaybackState.Playing,
                    Path.GetFileName(_currentFilePath)));
            }
            catch (UnsupportedCodecException ex)
            {
                Debug.WriteLine($"Playback error: {ex.Message}");
                _currentFilePath = null;
                _playbackState = PlaybackState.Stopped;
                RaisePlaybackStatusChanged(new PlaybackStatusEventArgs(
                    ex.Message,
                    Path.GetFileName(filePath)));
            }
        }
    }

    public void PausePlayback()
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_soundOut != null && _playbackState == PlaybackState.Playing)
            {
                try
                {
                    _soundOut.Pause();
                    _playbackState = PlaybackState.Paused;
                    RaisePlaybackStatusChanged(new PlaybackStatusEventArgs(
                        PlaybackState.Paused,
                        Path.GetFileName(_currentFilePath ?? string.Empty)));
                }
                catch (InvalidOperationException ex)
                {
                    Debug.WriteLine($"Error pausing playback: {ex.Message}");
                }
            }
        }
    }

    public void ResumePlayback()
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_soundOut != null && _playbackState == PlaybackState.Paused)
            {
                try
                {
                    _soundOut.Play();
                    _playbackState = PlaybackState.Playing;
                    RaisePlaybackStatusChanged(new PlaybackStatusEventArgs(
                        PlaybackState.Playing,
                        Path.GetFileName(_currentFilePath ?? string.Empty)));
                }
                catch (InvalidOperationException ex)
                {
                    Debug.WriteLine($"Error resuming playback: {ex.Message}");
                }
            }
        }
    }

    private void OnPlaybackStopped(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            if (_playbackState != PlaybackState.Stopped)
            {
                string? lastFile = _currentFilePath != null ? Path.GetFileName(_currentFilePath) : null;
                StopInternal(lastFile);
            }
        }
    }

    private void StopInternal(string? previousFileOverride = null)
    {
        if (_soundOut != null)
        {
            _soundOut.Stopped -= OnPlaybackStopped;
            try
            {
                _soundOut.Stop();
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine($"Error stopping sound: {ex.Message}");
            }

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
            RaisePlaybackStatusChanged(new PlaybackStatusEventArgs(
                PlaybackState.Stopped,
                fileForEvent ?? string.Empty));
        }
    }

    public void StopFile()
    {
        lock (_lock)
        {
            if (_playbackState == PlaybackState.Stopped && _soundOut == null && _waveSource == null)
            {
                return;
            }

            StopInternal();
        }
    }

    private void RaisePlaybackStatusChanged(PlaybackStatusEventArgs args)
    {
        PlaybackStatusChanged?.Invoke(this, args);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                lock (_lock)
                {
                    StopFile();
                }
            }

            _disposed = true;
        }
    }
}
