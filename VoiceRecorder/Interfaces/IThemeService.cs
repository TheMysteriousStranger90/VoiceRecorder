namespace VoiceRecorder.Interfaces;

public interface IThemeService
{
    ThemeVariant CurrentTheme { get; }
    void SetTheme(ThemeVariant theme);
    event EventHandler<ThemeChangedEventArgs>? ThemeChanged;
}

public enum ThemeVariant
{
    Main,
    Second
}

public class ThemeChangedEventArgs : EventArgs
{
    public ThemeVariant Theme { get; }

    public ThemeChangedEventArgs(ThemeVariant theme)
    {
        Theme = theme;
    }
}
