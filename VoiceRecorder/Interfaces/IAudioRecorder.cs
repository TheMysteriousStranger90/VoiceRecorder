using CSCore;
using CSCore.CoreAudioAPI;
using VoiceRecorder.Filters.Interfaces;

namespace VoiceRecorder.Interfaces;

public interface IAudioRecorder : IDisposable
{
    IWaveSource? CaptureSource { get; }
    event EventHandler? RecordingStarted;
    void StartRecording(string outputFilePath, MMDevice device, IAudioFilter? filter);
    void StopRecording();
    void UpdateSource(IWaveSource newSource);
}
