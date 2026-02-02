using System.Diagnostics.CodeAnalysis;

namespace VoiceRecorder.Models;

[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Model used across assemblies")]
public enum PlaybackState
{
    Stopped,
    Playing,
    Paused
}
