using VoiceRecorder.Interfaces;

namespace VoiceRecorder.Services;

internal sealed class ThemeService : IThemeService
{
    private ThemeVariant _currentTheme = ThemeVariant.Main;

    public ThemeVariant CurrentTheme => _currentTheme;

    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    public void SetTheme(ThemeVariant theme)
    {
        if (_currentTheme != theme)
        {
            _currentTheme = theme;
            ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(theme));
        }
    }
}
