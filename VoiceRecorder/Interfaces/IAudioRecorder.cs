using CSCore;
using CSCore.CoreAudioAPI;
using CSCore.Streams;
using VoiceRecorder.Filters.Interfaces;

namespace VoiceRecorder.Interfaces;

public interface IAudioRecorder : IDisposable
{
    IWaveSource? CaptureSource { get; }
    bool IsRecording { get; }
    event EventHandler? RecordingStarted;

    /// <summary>
    /// Инициализирует аудиоустройство (тяжелая операция).
    /// Вызывать при выборе устройства в UI.
    /// </summary>
    Task SetDeviceAsync(MMDevice device);

    /// <summary>
    /// Начинает запись в файл (быстрая операция).
    /// </summary>
    Task StartRecordingAsync(string outputFilePath, IAudioFilter? filter, CancellationToken cancellationToken = default);

    Task StopRecordingAsync(CancellationToken cancellationToken = default);
}
