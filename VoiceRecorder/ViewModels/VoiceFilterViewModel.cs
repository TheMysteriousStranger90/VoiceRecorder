using System.ComponentModel;
using CSCore;
using VoiceRecorder.Filters.Interfaces;

namespace VoiceRecorder.ViewModels;

public class VoiceFilterViewModel : INotifyPropertyChanged
{
    public IAudioFilter filterStrategy;
    private VoiceFilter voiceFilter;

    public VoiceFilterViewModel(AudioRecorder recorder, IAudioFilter filterStrategy)
    {
        this.filterStrategy = filterStrategy;
        voiceFilter = new VoiceFilter(recorder, filterStrategy);
    }

    public void ApplyFilter()
    {
        voiceFilter.ApplyFilter();
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString()
    {
        return filterStrategy.GetType().Name;
    }
}