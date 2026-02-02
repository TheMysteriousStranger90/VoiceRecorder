using System.Diagnostics.CodeAnalysis;
using VoiceRecorder.Models;
using VoiceRecorder.Models.Events;

namespace VoiceRecorder.Interfaces;

[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Interfaces used across assemblies")]
public interface IAudioPlayer : IDisposable
{
    float Volume { get; set; }
    PlaybackState CurrentPlaybackState { get; }
    TimeSpan Position { get; set; }
    TimeSpan Duration { get; }
    IReadOnlyList<int> EqualizerFrequencies { get; }

    event EventHandler<PlaybackStatusEventArgs>? PlaybackStatusChanged;
    event EventHandler<PlaybackProgressEventArgs>? PlaybackProgressChanged;

    Task PlayFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task PausePlaybackAsync(CancellationToken cancellationToken = default);
    Task ResumePlaybackAsync(CancellationToken cancellationToken = default);
    Task StopFileAsync(CancellationToken cancellationToken = default);
    Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default);
    Task SetPlaybackSpeedAsync(float speed, CancellationToken cancellationToken = default);

    void SetEqualizerBand(int bandIndex, float gainDb);
    float GetEqualizerBand(int bandIndex);
    void ResetEqualizer();
}
