using System;
using CSCore;
using VoiceRecorder.Filters.Interfaces;

public class VoiceFilter
{
    private AudioRecorder recorder;
    private IAudioFilter filterStrategy;

    public VoiceFilter(AudioRecorder recorder, IAudioFilter filterStrategy)
    {
        this.recorder = recorder;
        this.filterStrategy = filterStrategy;
    }

    public void ApplyFilter()
    {
        var source = recorder.CaptureSource;
        var filteredSource = filterStrategy.ApplyFilter(source);
        recorder.UpdateSource(filteredSource);
    }
}