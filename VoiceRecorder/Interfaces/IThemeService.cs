using System.Diagnostics.CodeAnalysis;
using VoiceRecorder.Models.Enums;
using VoiceRecorder.Models.Events;

namespace VoiceRecorder.Interfaces;

[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Interfaces used across assemblies")]
internal interface IThemeService
{
    ThemeVariant CurrentTheme { get; }
    void SetTheme(ThemeVariant theme);
    event EventHandler<ThemeChangedEventArgs>? ThemeChanged;
}
