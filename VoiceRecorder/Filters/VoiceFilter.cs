using VoiceRecorder.Filters.Interfaces;
using VoiceRecorder.Interfaces;

namespace VoiceRecorder.Filters;

internal sealed class VoiceFilter
{
    private readonly IAudioRecorder _recorder;
    private readonly IAudioFilter _filterStrategy;

    public VoiceFilter(IAudioRecorder recorder, IAudioFilter filterStrategy)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentNullException.ThrowIfNull(filterStrategy);

        _recorder = recorder;
        _filterStrategy = filterStrategy;
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
