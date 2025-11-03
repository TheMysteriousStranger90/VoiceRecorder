using VoiceRecorder.Filters.Interfaces;
using VoiceRecorder.Models;

namespace VoiceRecorder.Filters;

public class VoiceFilter
{
    private readonly AudioRecorder _recorder;
    private readonly IAudioFilter _filterStrategy;

    public VoiceFilter(AudioRecorder recorder, IAudioFilter filterStrategy)
    {
        this._recorder = recorder;
        this._filterStrategy = filterStrategy;
    }

    public void ApplyFilter()
    {
        var source = _recorder.CaptureSource;
        if (source == null)
        {
            throw new InvalidOperationException("Capture source cannot be null.");
        }

        var filteredSource = _filterStrategy.ApplyFilter(source);
        _recorder.UpdateSource(filteredSource);
    }
}
