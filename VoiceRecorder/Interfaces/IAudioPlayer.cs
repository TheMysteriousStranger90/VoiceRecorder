using VoiceRecorder.Models;
using PlaybackState = CSCore.SoundOut.PlaybackState;

namespace VoiceRecorder.Interfaces;

public interface IAudioPlayer : IDisposable
{
    float Volume { get; set; }
    Models.PlaybackState CurrentPlaybackState { get; }
    event EventHandler<PlaybackStatusEventArgs>? PlaybackStatusChanged;

    void PlayFile(string filePath);
    void PausePlayback();
    void ResumePlayback();
    void StopFile();
}
