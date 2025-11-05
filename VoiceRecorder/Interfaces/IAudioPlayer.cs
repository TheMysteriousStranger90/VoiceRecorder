using VoiceRecorder.Models;

namespace VoiceRecorder.Interfaces;

public interface IAudioPlayer : IDisposable
{
    float Volume { get; set; }
    PlaybackState CurrentPlaybackState { get; }
    event EventHandler<PlaybackStatusEventArgs>? PlaybackStatusChanged;

    Task PlayFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task PausePlaybackAsync(CancellationToken cancellationToken = default);
    Task ResumePlaybackAsync(CancellationToken cancellationToken = default);
    Task StopFileAsync(CancellationToken cancellationToken = default);
}
