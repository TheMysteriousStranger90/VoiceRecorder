using System.Diagnostics.CodeAnalysis;
using CSCore;
using CSCore.CoreAudioAPI;
using VoiceRecorder.Filters.Interfaces;
using VoiceRecorder.Models;
using VoiceRecorder.Models.Events;

namespace VoiceRecorder.Interfaces;

[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Interfaces used across assemblies")]
public interface IAudioRecorder : IDisposable
{
    bool IsRecording { get; }
    IWaveSource? CaptureSource { get; }
    float PeakLevel { get; }
    float AverageLevel { get; }
    TimeSpan RecordingDuration { get; }
    long BytesRecorded { get; }

    Task SetDeviceAsync(MMDevice device);

    Task StartRecordingAsync(
        string outputFilePath,
        IAudioFilter? filter,
        AudioSettings? settings = null,
        CancellationToken cancellationToken = default);

    Task StopRecordingAsync(CancellationToken cancellationToken = default);
    Task PauseRecordingAsync(CancellationToken cancellationToken = default);
    Task ResumeRecordingAsync(CancellationToken cancellationToken = default);

    event EventHandler? RecordingStarted;
    event EventHandler<AudioDataEventArgs>? AudioDataAvailable;
    event EventHandler<AudioLevelEventArgs>? AudioLevelChanged;
    event EventHandler<RecordingProgressEventArgs>? RecordingProgressChanged;
}
