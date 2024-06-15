using System.ComponentModel;
using CSCore;
using VoiceRecorder.Filters;
using VoiceRecorder.Filters.Interfaces;
using VoiceRecorder.Models;

namespace VoiceRecorder.ViewModels;

public sealed class VoiceFilterViewModel : INotifyPropertyChanged
{
    public readonly IAudioFilter FilterStrategy;
    private readonly VoiceFilter _voiceFilter;

    public VoiceFilterViewModel(AudioRecorder recorder, IAudioFilter filterStrategy)
    {
        this.FilterStrategy = filterStrategy;
        _voiceFilter = new VoiceFilter(recorder, filterStrategy);
    }

    public void ApplyFilter()
    {
        _voiceFilter.ApplyFilter();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString()
    {
        return FilterStrategy?.GetType().Name ?? "Without Filters";
    }
}