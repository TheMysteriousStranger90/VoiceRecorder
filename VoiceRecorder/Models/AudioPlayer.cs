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
    private bool _isPlaying;

    public bool IsPlaying => _isPlaying;
    public event EventHandler<PlaybackStatusEventArgs> PlaybackStatusChanged;

    public void PlayFile(string filePath)
    {
        Stop();
        
        try
        {
            _waveSource = CodecFactory.Instance.GetCodec(filePath);
            _soundOut = new WasapiOut();
            _soundOut.Initialize(_waveSource);
            _soundOut.Play();
            _isPlaying = true;
            
            PlaybackStatusChanged?.Invoke(this, new PlaybackStatusEventArgs(true, Path.GetFileName(filePath)));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Playback error: {ex.Message}");
            PlaybackStatusChanged?.Invoke(this, new PlaybackStatusEventArgs(false, null, ex.Message));
        }
    }

    public void Stop()
    {
        if (_soundOut != null)
        {
            _soundOut.Stop();
            _soundOut.Dispose();
            _soundOut = null;
        }

        if (_waveSource != null)
        {
            _waveSource.Dispose();
            _waveSource = null;
        }

        _isPlaying = false;
        PlaybackStatusChanged?.Invoke(this, new PlaybackStatusEventArgs(false, null));
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