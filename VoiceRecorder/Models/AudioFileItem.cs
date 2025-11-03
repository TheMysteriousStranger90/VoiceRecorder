using System.Globalization;
using CSCore;
using CSCore.Codecs;
using ReactiveUI;
using VoiceRecorder.Exceptions;

namespace VoiceRecorder.Models;

internal sealed class AudioFileItem : ReactiveObject
{
    private string _name;
    private string _relativePath;
    private DateTime _dateCreated;
    private long _size;
    private TimeSpan _duration;

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public string RelativePath
    {
        get => _relativePath;
        set => this.RaiseAndSetIfChanged(ref _relativePath, value);
    }

    public string FullPath { get; }

    public DateTime DateCreated
    {
        get => _dateCreated;
        set => this.RaiseAndSetIfChanged(ref _dateCreated, value);
    }

    public long Size
    {
        get => _size;
        set => this.RaiseAndSetIfChanged(ref _size, value);
    }

    public string FormattedSize => $"{(_size / 1024.0).ToString("F2", CultureInfo.InvariantCulture)} KB";

    public TimeSpan Duration
    {
        get => _duration;
        set => this.RaiseAndSetIfChanged(ref _duration, value);
    }

    public string FormattedDuration => _duration.ToString(@"mm\:ss", CultureInfo.InvariantCulture);

    public AudioFileItem(string fullPath, string basePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);

        FullPath = fullPath;
        _name = Path.GetFileName(fullPath);
        _relativePath = fullPath.Substring(basePath.Length +
                                           (basePath.EndsWith(Path.DirectorySeparatorChar.ToString(),
                                               StringComparison.Ordinal)
                                               ? 0
                                               : 1));

        var fileInfo = new FileInfo(fullPath);
        _dateCreated = fileInfo.CreationTime;
        _size = fileInfo.Length;

        try
        {
            using IWaveSource waveSource = CodecFactory.Instance.GetCodec(fullPath);
            _duration = waveSource.GetLength();
        }
        catch (UnsupportedCodecException)
        {
            _duration = TimeSpan.Zero;
        }
    }
}
