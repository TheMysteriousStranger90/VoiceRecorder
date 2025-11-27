using CSCore;
using CSCore.CoreAudioAPI;
using VoiceRecorder.Filters.Interfaces;
using VoiceRecorder.Models;

namespace VoiceRecorder.Interfaces;

public interface IAudioRecorder : IDisposable
{
    IWaveSource? CaptureSource { get; }
    bool IsRecording { get; }

    event EventHandler? RecordingStarted;
    event EventHandler<AudioDataEventArgs>? AudioDataAvailable;

    Task SetDeviceAsync(MMDevice device);
    Task StartRecordingAsync(string outputFilePath, IAudioFilter? filter,
        AudioSettings? settings = null, CancellationToken cancellationToken = default);
    Task StopRecordingAsync(CancellationToken cancellationToken = default);
}
