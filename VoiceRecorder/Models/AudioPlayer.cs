using System;
using System.Diagnostics;
using System.IO;
using CSCore;
using CSCore.Codecs;
using CSCore.SoundOut;

namespace VoiceRecorder.Models;

public class AudioPlayer : IDisposable
{
    private IWaveSource _waveSource;
    private ISoundOut _soundOut;
    private bool _disposed;
    private float _volume = 1.0f;
    private PlaybackState _playbackState = PlaybackState.Stopped;
    private string _currentFilePath;

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

    public event EventHandler<PlaybackStatusEventArgs> PlaybackStatusChanged;

    public void PlayFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;

        Stop();

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
            
            PlaybackStatusChanged?.Invoke(this, new PlaybackStatusEventArgs(PlaybackState.Playing, Path.GetFileName(_currentFilePath)));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Playback error: {ex.Message}");
            _currentFilePath = null;
            _playbackState = PlaybackState.Stopped;
            PlaybackStatusChanged?.Invoke(this, new PlaybackStatusEventArgs(ex.Message, Path.GetFileName(filePath)));
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
                PlaybackStatusChanged?.Invoke(this, new PlaybackStatusEventArgs(PlaybackState.Paused, Path.GetFileName(_currentFilePath)));
            }
            catch (Exception ex)
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
                PlaybackStatusChanged?.Invoke(this, new PlaybackStatusEventArgs(PlaybackState.Playing, Path.GetFileName(_currentFilePath)));
            }
            catch (Exception ex)
            {
                 Debug.WriteLine($"Error resuming playback: {ex.Message}");
            }
        }
    }
    
    private void OnPlaybackStopped(object sender, EventArgs e)
    {
        if (_playbackState != PlaybackState.Stopped)
        {
            string lastFile = _currentFilePath != null ? Path.GetFileName(_currentFilePath) : null;
            
            StopInternal(true, lastFile);
        }
    }
    
    private void StopInternal(bool fromPlaybackEnd, string previousFileOverride = null)
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

        string fileForEvent = previousFileOverride ?? (_currentFilePath != null ? Path.GetFileName(_currentFilePath) : null);
        _currentFilePath = null;

        if (_playbackState != PlaybackState.Stopped)
        {
            _playbackState = PlaybackState.Stopped;
            PlaybackStatusChanged?.Invoke(this, new PlaybackStatusEventArgs(PlaybackState.Stopped, fileForEvent));
        }
        else if (fileForEvent != null && _soundOut == null && _waveSource == null)
        {
        }
    }


    public void Stop()
    {
        if (_playbackState == PlaybackState.Stopped && _soundOut == null && _waveSource == null)
        {
            return;
        }
        StopInternal(false);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
    }
}