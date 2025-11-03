using System.ComponentModel;
using VoiceRecorder.Filters;
using VoiceRecorder.Filters.Interfaces;
using VoiceRecorder.Interfaces;

namespace VoiceRecorder.ViewModels;

internal sealed class VoiceFilterViewModel : INotifyPropertyChanged
{
    private readonly IAudioFilter? _filterStrategy;
    private readonly VoiceFilter? _voiceFilter;

    public IAudioFilter? FilterStrategy => _filterStrategy;

    public VoiceFilterViewModel(IAudioRecorder? recorder, IAudioFilter? filterStrategy)
    {
        _filterStrategy = filterStrategy;
        if (recorder != null && filterStrategy != null)
        {
            _voiceFilter = new VoiceFilter(recorder, filterStrategy);
        }
    }

    public void ApplyFilter()
    {
        _voiceFilter?.ApplyFilter();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString()
    {
        return _filterStrategy?.GetType().Name.Replace("Filter", string.Empty, StringComparison.Ordinal) ??
               "Without Filters";
    }
}
