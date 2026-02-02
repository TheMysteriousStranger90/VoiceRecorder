using System.Diagnostics.CodeAnalysis;

namespace VoiceRecorder.Models.Events;

[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "EventArgs used across assemblies")]
public sealed class AudioLevelEventArgs : EventArgs
{
    public float PeakLevel { get; }
    public float AverageLevel { get; }
    public float PeakLevelDb => 20f * MathF.Log10(Math.Max(PeakLevel, 0.00001f));
    public float AverageLevelDb => 20f * MathF.Log10(Math.Max(AverageLevel, 0.00001f));

    public AudioLevelEventArgs(float peakLevel, float averageLevel)
    {
        PeakLevel = peakLevel;
        AverageLevel = averageLevel;
    }
}
