using CSCore;
using CSCore.CoreAudioAPI;
using CSCore.Streams;
using VoiceRecorder.Filters.Interfaces;
using VoiceRecorder.Models;

namespace VoiceRecorder.Interfaces;

public interface IAudioRecorder : IDisposable
{
    IWaveSource? CaptureSource { get; }
    bool IsRecording { get; }
    event EventHandler? RecordingStarted;
    Task SetDeviceAsync(MMDevice device);
    Task StartRecordingAsync(string outputFilePath, IAudioFilter? filter, AudioSettings? settings, CancellationToken cancellationToken = default);
    Task StopRecordingAsync(CancellationToken cancellationToken = default);
}
