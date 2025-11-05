using CSCore;
using CSCore.CoreAudioAPI;
using VoiceRecorder.Filters.Interfaces;

namespace VoiceRecorder.Interfaces;

public interface IAudioRecorder : IDisposable
{
    IWaveSource? CaptureSource { get; }
    bool IsRecording { get; }
    event EventHandler? RecordingStarted;

    Task StartRecordingAsync(string outputFilePath, MMDevice device, IAudioFilter? filter,
        CancellationToken cancellationToken = default);

    Task StopRecordingAsync(CancellationToken cancellationToken = default);
    void UpdateSource(IWaveSource newSource);
}
