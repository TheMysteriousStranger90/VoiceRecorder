using System.Globalization;
using System.Text.RegularExpressions;

namespace VoiceRecorder.Utils;

public static partial class AudioFilePathHelper
{
    [GeneratedRegex(@"[<>:""/\\|?*]", RegexOptions.Compiled)]
    private static partial Regex InvalidFileNameCharsRegex();

    public static string GenerateAudioFilePath(string deviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);

        string basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "AzioVoiceRecorder");

        string sanitizedDeviceName = SanitizeFileName(deviceName);
        string deviceFolder = Path.Combine(basePath, sanitizedDeviceName);

        if (!Directory.Exists(deviceFolder))
        {
            Directory.CreateDirectory(deviceFolder);
        }

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        string fileName = $"Recording_{timestamp}.wav";

        return Path.Combine(deviceFolder, fileName);
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "Default";
        }

        string sanitized = InvalidFileNameCharsRegex().Replace(fileName, "_");

        if (sanitized.Length > 50)
        {
            sanitized = sanitized[..50];
        }

        return sanitized.Trim();
    }
}
