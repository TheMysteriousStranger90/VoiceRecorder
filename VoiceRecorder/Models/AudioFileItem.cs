using System;
using System.IO;
using CSCore;
using CSCore.Codecs;
using ReactiveUI;

namespace VoiceRecorder.Models;

public class AudioFileItem : ReactiveObject
{
    private string _name;

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    private string _relativePath;

    public string RelativePath
    {
        get => _relativePath;
        set => this.RaiseAndSetIfChanged(ref _relativePath, value);
    }

    public string FullPath { get; }

    private DateTime _dateCreated;

    public DateTime DateCreated
    {
        get => _dateCreated;
        set => this.RaiseAndSetIfChanged(ref _dateCreated, value);
    }

    private long _size;

    public long Size
    {
        get => _size;
        set => this.RaiseAndSetIfChanged(ref _size, value);
    }

    public string FormattedSize => $"{(_size / 1024.0):F2} KB";

    private TimeSpan _duration;

    public TimeSpan Duration
    {
        get => _duration;
        set => this.RaiseAndSetIfChanged(ref _duration, value);
    }

    public string FormattedDuration => $"{_duration:mm\\:ss}";

    public AudioFileItem(string fullPath, string basePath)
    {
        FullPath = fullPath;
        Name = Path.GetFileName(fullPath);
        RelativePath =
            fullPath.Substring(basePath.Length + (basePath.EndsWith(Path.DirectorySeparatorChar.ToString()) ? 0 : 1));

        var fileInfo = new FileInfo(fullPath);
        DateCreated = fileInfo.CreationTime;
        Size = fileInfo.Length;

        try
        {
            using (IWaveSource waveSource = CodecFactory.Instance.GetCodec(fullPath))
            {
                Duration = waveSource.GetLength();
            }
        }
        catch (Exception)
        {
            Duration = TimeSpan.Zero;
        }
    }
}