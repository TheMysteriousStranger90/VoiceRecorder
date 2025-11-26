using VoiceRecorder.Models;

namespace VoiceRecorder.Interfaces;

public interface IAudioPlayer : IDisposable
{
    float Volume { get; set; }
    PlaybackState CurrentPlaybackState { get; }
    TimeSpan Position { get; set; }
    TimeSpan Duration { get; }

    event EventHandler<PlaybackStatusEventArgs>? PlaybackStatusChanged;
    event EventHandler<PlaybackProgressEventArgs>? PlaybackProgressChanged;

    Task PlayFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task PausePlaybackAsync(CancellationToken cancellationToken = default);
    Task ResumePlaybackAsync(CancellationToken cancellationToken = default);
    Task StopFileAsync(CancellationToken cancellationToken = default);
    Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default);
}
