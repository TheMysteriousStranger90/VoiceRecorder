using VoiceRecorder.Models.Enums;

namespace VoiceRecorder.Models.Events;

internal sealed class ThemeChangedEventArgs : EventArgs
{
    public ThemeVariant Theme { get; }

    public ThemeChangedEventArgs(ThemeVariant theme)
    {
        Theme = theme;
    }
}
