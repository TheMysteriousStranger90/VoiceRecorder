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

    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance")]
    private ISoundOut? _soundOut;

    private bool _disposed;
    private float _volume = 1.0f;
    private PlaybackState _playbackState = PlaybackState.Stopped;
    private string? _currentFilePath;
    private readonly SemaphoreSlim _playbackLock = new(1, 1);
    private CancellationTokenSource? _playbackCts;
    private System.Threading.Timer? _progressTimer;

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

    public TimeSpan Position
    {
        get
        {
            if (_waveSource != null && _waveSource.CanSeek)
            {
                return _waveSource.GetPosition();
            }

            return TimeSpan.Zero;
        }
        set
        {
            if (_waveSource != null && _waveSource.CanSeek)
            {
                _waveSource.SetPosition(value);
            }
        }
    }

    public TimeSpan Duration
    {
        get
        {
            if (_waveSource != null && _waveSource.CanSeek)
            {
                return _waveSource.GetLength();
            }

            return TimeSpan.Zero;
        }
    }

    public event EventHandler<PlaybackStatusEventArgs>? PlaybackStatusChanged;
    public event EventHandler<PlaybackProgressEventArgs>? PlaybackProgressChanged;

    public async Task PlayFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        await _playbackLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopFileInternalAsync(cancellationToken).ConfigureAwait(false);

            _playbackCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                _currentFilePath = filePath;

                await Task.Run(() =>
                {
                    _waveSource = CodecFactory.Instance.GetCodec(_currentFilePath);
                    _soundOut = new WasapiOut();
                    _soundOut.Stopped += OnPlaybackStopped;
                    _soundOut.Initialize(_waveSource);
                    _soundOut.Volume = _volume;
                    _soundOut.Play();
                }, _playbackCts.Token).ConfigureAwait(false);

                _playbackState = PlaybackState.Playing;

                StartProgressTimer();

                PlaybackStatusChanged?.Invoke(this, new PlaybackStatusEventArgs(
                    PlaybackState.Playing,
                    Path.GetFileName(_currentFilePath)));
            }
            catch (OperationCanceledException)
            {
                _currentFilePath = null;
                _playbackState = PlaybackState.Stopped;
                throw;
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
        finally
        {
            _playbackLock.Release();
        }
    }

    public async Task PausePlaybackAsync(CancellationToken cancellationToken = default)
    {
        await _playbackLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_soundOut != null && _playbackState == PlaybackState.Playing)
            {
                await Task.Run(() =>
                {
                    try
                    {
                        _soundOut.Pause();
                        _playbackState = PlaybackState.Paused;
                        StopProgressTimer();
                        PlaybackStatusChanged?.Invoke(this, new PlaybackStatusEventArgs(
                            PlaybackState.Paused,
                            Path.GetFileName(_currentFilePath ?? string.Empty)));
                    }
                    catch (InvalidOperationException ex)
                    {
                        Debug.WriteLine($"Error pausing playback: {ex.Message}");
                    }
                }, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _playbackLock.Release();
        }
    }

    public async Task ResumePlaybackAsync(CancellationToken cancellationToken = default)
    {
        await _playbackLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_soundOut != null && _playbackState == PlaybackState.Paused)
            {
                await Task.Run(() =>
                {
                    try
                    {
                        _soundOut.Play();
                        _playbackState = PlaybackState.Playing;
                        StartProgressTimer();
                        PlaybackStatusChanged?.Invoke(this, new PlaybackStatusEventArgs(
                            PlaybackState.Playing,
                            Path.GetFileName(_currentFilePath ?? string.Empty)));
                    }
                    catch (InvalidOperationException ex)
                    {
                        Debug.WriteLine($"Error resuming playback: {ex.Message}");
                    }
                }, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _playbackLock.Release();
        }
    }

    public async Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
    {
        await _playbackLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_waveSource != null && _waveSource.CanSeek)
            {
                await Task.Run(() =>
                {
                    Position = position;
                    NotifyProgressUpdate();
                }, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _playbackLock.Release();
        }
    }

    private void StartProgressTimer()
    {
        StopProgressTimer();
        _progressTimer = new System.Threading.Timer(_ => NotifyProgressUpdate(), null, TimeSpan.Zero,
            TimeSpan.FromMilliseconds(100));
    }

    private void StopProgressTimer()
    {
        _progressTimer?.Dispose();
        _progressTimer = null;
    }

    private void NotifyProgressUpdate()
    {
        if (_waveSource != null && _playbackState == PlaybackState.Playing)
        {
            var position = Position;
            var duration = Duration;
            PlaybackProgressChanged?.Invoke(this, new PlaybackProgressEventArgs(position, duration));
        }
    }

    private async void OnPlaybackStopped(object? sender, EventArgs e)
    {
        if (_playbackState != PlaybackState.Stopped)
        {
            StopProgressTimer();
            string? lastFile = _currentFilePath != null ? Path.GetFileName(_currentFilePath) : null;
            await StopInternalAsync(lastFile, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task StopInternalAsync(string? previousFileOverride = null,
        CancellationToken cancellationToken = default)
    {
        StopProgressTimer();

        await Task.Run(() =>
        {
            if (cancellationToken.IsCancellationRequested)
                return;

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
        }, cancellationToken).ConfigureAwait(false);

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

    private async Task StopFileInternalAsync(CancellationToken cancellationToken = default)
    {
        if (_playbackState == PlaybackState.Stopped && _soundOut == null && _waveSource == null)
        {
            return;
        }

        await ((_playbackCts?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false));

        await StopInternalAsync(null, cancellationToken).ConfigureAwait(false);
    }

    public async Task StopFileAsync(CancellationToken cancellationToken = default)
    {
        await _playbackLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopFileInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _playbackLock.Release();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            StopProgressTimer();
            StopFileAsync().GetAwaiter().GetResult();
            _playbackCts?.Dispose();
            _playbackLock.Dispose();
            _disposed = true;
        }
    }
}
