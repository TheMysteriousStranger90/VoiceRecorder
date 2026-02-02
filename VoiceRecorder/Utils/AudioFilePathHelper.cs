using System.Globalization;
using VoiceRecorder.Models;
using VoiceRecorder.Models.Enums;

namespace VoiceRecorder.Utils;

internal static partial class AudioFilePathHelper
{
    public static string GenerateAudioFilePath(string deviceName, OutputFormat format = OutputFormat.Wav)
    {
        string basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "AzioVoiceRecorder");

        string safeDeviceName = SanitizeDeviceName(deviceName);
        string deviceFolder = Path.Combine(basePath, safeDeviceName);

        if (!Directory.Exists(deviceFolder))
        {
            Directory.CreateDirectory(deviceFolder);
        }

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        string extension = GetExtensionForFormat(format);
        string fileName = $"Recording_{timestamp}{extension}";

        return Path.Combine(deviceFolder, fileName);
    }

    public static string GenerateAudioFilePath(string deviceName, AudioSettings settings)
    {
        return GenerateAudioFilePath(deviceName, settings.OutputFormat);
    }

    private static string GetExtensionForFormat(OutputFormat format)
    {
        return format switch
        {
            OutputFormat.Wav => ".wav",
            OutputFormat.Mp3 => ".mp3",
            OutputFormat.Aac => ".m4a",
            OutputFormat.Flac => ".flac",
            _ => ".wav"
        };
    }

    private static string SanitizeDeviceName(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return "Unknown";
        }

        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = new(deviceName
            .Select(c => invalidChars.Contains(c) ? '_' : c)
            .ToArray());

        sanitized = sanitized.Trim().TrimEnd('.');

        if (sanitized.Length > 50)
        {
            sanitized = sanitized[..50];
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "Unknown" : sanitized;
    }
}
