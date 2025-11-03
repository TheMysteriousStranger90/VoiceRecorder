namespace VoiceRecorder.Utils;

public static class AudioFilePathHelper
{
    public static string GenerateAudioFilePath(string deviceName)
    {
        string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "VoiceRecorder");
        Directory.CreateDirectory(defaultPath);

        string deviceFolderPath = Path.Combine(defaultPath, deviceName);
        Directory.CreateDirectory(deviceFolderPath);

        string fileName = $"output_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
        string filePath = Path.Combine(deviceFolderPath, fileName);

        return filePath;
    }
}
