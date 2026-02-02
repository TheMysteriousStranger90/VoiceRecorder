using System.Diagnostics.CodeAnalysis;

namespace VoiceRecorder.Models.Events;

[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "EventArgs used across assemblies")]
public sealed class AudioDataEventArgs : EventArgs
{
    [SuppressMessage("Performance", "CA1819:Properties should not return arrays",
        Justification = "Array needed for audio sample data performance")]
    public float[] Samples { get; }

    public AudioDataEventArgs(float[] samples)
    {
        Samples = samples;
    }
}
