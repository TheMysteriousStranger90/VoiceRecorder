using System.ComponentModel;
using VoiceRecorder.Filters;
using VoiceRecorder.Filters.Interfaces;

namespace VoiceRecorder.ViewModels;

internal sealed class VoiceFilterViewModel : INotifyPropertyChanged
{
    public string DisplayName { get; }
    public string Description { get; }
    public IAudioFilter? FilterStrategy { get; }

    public VoiceFilterViewModel(string displayName, string description, IAudioFilter? filterStrategy)
    {
        DisplayName = displayName;
        Description = description;
        FilterStrategy = filterStrategy;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString() => DisplayName;

    public static IReadOnlyList<VoiceFilterViewModel> GetAvailableFilters()
    {
        return
        [
            new VoiceFilterViewModel("None", "No audio effect applied", null),

            // Reverb presets
            new VoiceFilterViewModel("Reverb - Small Room", "Intimate room acoustics", FilterPresets.SmallRoom),
            new VoiceFilterViewModel("Reverb - Large Hall", "Spacious concert hall", FilterPresets.LargeHall),
            new VoiceFilterViewModel("Reverb - Cathedral", "Very long decay, church-like", FilterPresets.Cathedral),

            // Echo presets
            new VoiceFilterViewModel("Echo - Subtle", "Slight repetition", FilterPresets.SubtleEcho),
            new VoiceFilterViewModel("Echo - Slapback", "Rockabilly style quick echo", FilterPresets.SlapbackEcho),
            new VoiceFilterViewModel("Echo - Long", "Spacey long delay", FilterPresets.LongEcho),

            // Chorus presets
            new VoiceFilterViewModel("Chorus - Rich", "Thick, lush sound", FilterPresets.RichChorus),
            new VoiceFilterViewModel("Chorus - Subtle", "Light enhancement", FilterPresets.SubtleChorus),

            // Flanger presets
            new VoiceFilterViewModel("Flanger - Jet", "Classic sweeping sound", FilterPresets.JetFlanger),
            new VoiceFilterViewModel("Flanger - Gentle", "Subtle movement", FilterPresets.GentleFlanger),

            // Distortion presets
            new VoiceFilterViewModel("Distortion - Warm", "Adds analog warmth", FilterPresets.WarmDistortion),
            new VoiceFilterViewModel("Distortion - Heavy", "Aggressive edge", FilterPresets.HeavyDistortion),

            // Compressor presets
            new VoiceFilterViewModel("Compressor - Voice", "Optimized for vocals/podcasts", FilterPresets.VoiceCompressor),
            new VoiceFilterViewModel("Compressor - Gentle", "Subtle leveling", FilterPresets.GentleCompression),
            new VoiceFilterViewModel("Compressor - Broadcast", "Radio broadcast style", FilterPresets.BroadcastCompressor),

            // Special effects
            new VoiceFilterViewModel("Robot Voice", "Robotic modulation effect", FilterPresets.RobotVoice),
            new VoiceFilterViewModel("Tremolo", "Classic tremolo effect", FilterPresets.Tremolo),

            // Chains
            new VoiceFilterViewModel("Professional Vocal", "Compression + subtle reverb", FilterPresets.ProfessionalVocal),
            new VoiceFilterViewModel("Atmospheric", "Chorus + reverb + echo", FilterPresets.Atmospheric),
            new VoiceFilterViewModel("Vintage Radio", "Warm distortion + compression", FilterPresets.VintageRadio)
        ];
    }
}
