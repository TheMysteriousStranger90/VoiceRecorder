using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace VoiceRecorder.Models.Events;

[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "EventArgs used across assemblies")]
public sealed class RecordingProgressEventArgs : EventArgs
{
    public TimeSpan Duration { get; }
    public long BytesRecorded { get; }
    public float PeakLevel { get; }
    public float AverageLevel { get; }

    public string FormattedDuration => Duration.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
    public string FormattedSize => FormatBytes(BytesRecorded);

    public RecordingProgressEventArgs(TimeSpan duration, long bytesRecorded, float peakLevel, float averageLevel)
    {
        Duration = duration;
        BytesRecorded = bytesRecorded;
        PeakLevel = peakLevel;
        AverageLevel = averageLevel;
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB"];
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return string.Format(CultureInfo.InvariantCulture, "{0:0.##} {1}", size, suffixes[order]);
    }
}
