using System.ComponentModel;
using VoiceRecorder.Filters.Interfaces;

namespace VoiceRecorder.ViewModels;

internal sealed class VoiceFilterViewModel : INotifyPropertyChanged
{
    public IAudioFilter? FilterStrategy { get; }

    public VoiceFilterViewModel(IAudioFilter? filterStrategy)
    {
        FilterStrategy = filterStrategy;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString()
    {
        return FilterStrategy?.GetType().Name.Replace("Filter", string.Empty, StringComparison.Ordinal) ??
               "Without Filters";
    }
}
