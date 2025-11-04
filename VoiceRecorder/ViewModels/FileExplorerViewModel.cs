using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using VoiceRecorder.Interfaces;
using VoiceRecorder.Models;
using VoiceRecorder.Services;

namespace VoiceRecorder.ViewModels;

internal sealed class FileExplorerViewModel : ViewModelBase, IDisposable
{
    private readonly IAudioPlayer _player;
    private string _currentPlayingFile = string.Empty;
    private bool _isPlaying;
    private bool _isActuallyPlaying;
    private string _playbackStatus = string.Empty;
    private float _volume = 1.0f;
    private bool _disposed;
    private CancellationTokenSource? _loadCancellationTokenSource;

    public bool IsPlaying
    {
        get => _isPlaying;
        private set => this.RaiseAndSetIfChanged(ref _isPlaying, value);
    }

    public bool IsActuallyPlaying
    {
        get => _isActuallyPlaying;
        private set => this.RaiseAndSetIfChanged(ref _isActuallyPlaying, value);
    }

    public string CurrentPlayingFile
    {
        get => _currentPlayingFile;
        private set => this.RaiseAndSetIfChanged(ref _currentPlayingFile, value);
    }

    public string PlaybackStatus
    {
        get => _playbackStatus;
        private set => this.RaiseAndSetIfChanged(ref _playbackStatus, value);
    }

    public float Volume
    {
        get => _volume;
        set
        {
            this.RaiseAndSetIfChanged(ref _volume, value);
            _player.Volume = value;
        }
    }

    public ObservableCollection<string> Folders { get; } = [];
    public ObservableCollection<AudioFileItem> Files { get; } = [];

    public ReactiveCommand<AudioFileItem, Unit> OpenFileCommand { get; }
    public ReactiveCommand<AudioFileItem, Unit> PlayFileCommand { get; }
    public ReactiveCommand<AudioFileItem, Unit> DeleteFileCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshFilesCommand { get; }
    public ICommand PlayPauseCommand { get; }
    public ICommand StopCommand { get; }

    public event EventHandler<StatusChangedEventArgs>? StatusChanged;

    private void UpdateStatus(string message)
    {
        StatusChanged?.Invoke(this, new StatusChangedEventArgs(message));
    }

    public FileExplorerViewModel(IAudioPlayer? player = null)
    {
        _player = player ?? new AudioPlayer();
        _player.PlaybackStatusChanged += OnPlaybackStatusChanged;

        OpenFileCommand = ReactiveCommand.Create<AudioFileItem>(OpenFile);
        PlayFileCommand = ReactiveCommand.Create<AudioFileItem>(PlayFile);
        DeleteFileCommand = ReactiveCommand.Create<AudioFileItem>(DeleteFile);
        RefreshFilesCommand = ReactiveCommand.Create(LoadFoldersAndFiles);
        PlayPauseCommand = ReactiveCommand.Create(PlayPause);
        StopCommand = ReactiveCommand.Create(Stop);

        LoadFoldersAndFiles();
    }

    public void LoadFoldersAndFiles()
    {
        _loadCancellationTokenSource?.Cancel();
        _loadCancellationTokenSource?.Dispose();
        _loadCancellationTokenSource = new CancellationTokenSource();

        var cancellationToken = _loadCancellationTokenSource.Token;

        UpdateStatus("Loading files...");

        Dispatcher.UIThread.Post(() =>
        {
            Files.Clear();
            Folders.Clear();
        });

        string basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "VoiceRecorder");

        if (!Directory.Exists(basePath))
        {
            try
            {
                Directory.CreateDirectory(basePath);
                UpdateStatus("Created VoiceRecorder directory.");
            }
            catch (IOException ioEx)
            {
                UpdateStatus($"I/O error creating directory: {ioEx.Message}");
                Debug.WriteLine($"I/O error creating directory: {ioEx.Message}");
            }
            catch (UnauthorizedAccessException uaEx)
            {
                UpdateStatus($"Access denied creating directory: {uaEx.Message}");
                Debug.WriteLine($"Access denied creating directory: {uaEx.Message}");
            }
            catch (NotSupportedException nsEx)
            {
                UpdateStatus($"Invalid path for directory: {nsEx.Message}");
                Debug.WriteLine($"Invalid path: {nsEx.Message}");
            }

            return;
        }

        Observable.Start(() =>
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                        return (new List<string>(), new List<AudioFileItem>());

                    var directories = Directory.GetDirectories(basePath, "*", SearchOption.AllDirectories)
                        .Select(d => Path.GetRelativePath(basePath, d))
                        .ToList();

                    if (cancellationToken.IsCancellationRequested)
                        return (directories, new List<AudioFileItem>());

                    var loadedFiles = new List<AudioFileItem>();
                    var files = Directory.GetFiles(basePath, "*.wav", SearchOption.AllDirectories);

                    foreach (var file in files)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        try
                        {
                            loadedFiles.Add(new AudioFileItem(file, basePath));
                        }
                        catch (ArgumentException argEx)
                        {
                            Debug.WriteLine($"Invalid argument loading file info for {file}: {argEx.Message}");
                        }
                        catch (IOException ioEx)
                        {
                            Debug.WriteLine($"I/O error loading file info for {file}: {ioEx.Message}");
                        }
                        catch (UnauthorizedAccessException uaEx)
                        {
                            Debug.WriteLine($"Access denied loading file info for {file}: {uaEx.Message}");
                        }
                    }

                    return (directories, loadedFiles.OrderByDescending(f => f.DateCreated).ToList());
                }
                catch (IOException ioEx)
                {
                    Debug.WriteLine($"I/O error in LoadFoldersAndFiles: {ioEx.Message}");
                    return (new List<string>(), new List<AudioFileItem>());
                }
                catch (UnauthorizedAccessException uaEx)
                {
                    Debug.WriteLine($"Access denied in LoadFoldersAndFiles: {uaEx.Message}");
                    return (new List<string>(), new List<AudioFileItem>());
                }
            }, RxApp.TaskpoolScheduler)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(result =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                var (directories, loadedFiles) = result;

                foreach (var directory in directories)
                {
                    Folders.Add(directory);
                }

                foreach (var fileItem in loadedFiles)
                {
                    Files.Add(fileItem);
                }

                UpdateStatus(Files.Count != 0 ? $"{Files.Count} file(s) loaded." : "No audio files found.");
            }, ex =>
            {
                UpdateStatus($"Error loading files: {ex.Message}");
                Debug.WriteLine($"Error in LoadFoldersAndFiles observable: {ex.Message}");
            });
    }

    private void DeleteFile(AudioFileItem? fileItem)
    {
        if (fileItem == null) return;

        Debug.WriteLine(string.Format(CultureInfo.InvariantCulture, "Attempting to delete: {0}", fileItem.FullPath));

        try
        {
            if (IsPlaying && CurrentPlayingFile == fileItem.Name)
            {
                Stop();
            }

            if (!File.Exists(fileItem.FullPath))
            {
                UpdateStatus($"File '{fileItem.Name}' not found.");
                Files.Remove(fileItem);
                return;
            }

            File.Delete(fileItem.FullPath);
            Files.Remove(fileItem);
            UpdateStatus($"File '{fileItem.Name}' deleted.");
        }
        catch (IOException ioEx)
        {
            UpdateStatus($"I/O error deleting file '{fileItem.Name}': {ioEx.Message}");
            Debug.WriteLine($"I/O error deleting file: {ioEx.Message}");
        }
        catch (UnauthorizedAccessException uaEx)
        {
            UpdateStatus($"Access denied deleting file '{fileItem.Name}': {uaEx.Message}");
            Debug.WriteLine($"Access denied deleting file: {uaEx.Message}");
        }
    }

    private void PlayPause()
    {
        if (IsPlaying)
        {
            _player.StopFile();
        }
        else if (!string.IsNullOrEmpty(CurrentPlayingFile))
        {
            var fileToPlay = Files.FirstOrDefault(f => f.Name == CurrentPlayingFile);
            if (fileToPlay != null)
            {
                PlayFile(fileToPlay);
            }
        }
    }

    private void Stop()
    {
        _player.StopFile();
    }

    private void OpenFile(AudioFileItem? fileItem)
    {
        if (fileItem == null) return;

        if (!File.Exists(fileItem.FullPath))
        {
            UpdateStatus($"File not found: {fileItem.Name}");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(fileItem.FullPath) { UseShellExecute = true });
        }
        catch (Win32Exception w32Ex)
        {
            UpdateStatus($"Error opening file: {w32Ex.Message}");
            Debug.WriteLine($"Win32 error opening file: {w32Ex.Message}");
        }
        catch (FileNotFoundException fnfEx)
        {
            UpdateStatus($"File not found: {fnfEx.Message}");
            Debug.WriteLine($"File not found: {fnfEx.Message}");
        }
        catch (IOException ioEx)
        {
            UpdateStatus($"I/O error opening file: {ioEx.Message}");
            Debug.WriteLine($"I/O error opening file: {ioEx.Message}");
        }
    }

    private void OnPlaybackStatusChanged(object? sender, PlaybackStatusEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CurrentPlayingFile = e.FileName;
            IsPlaying = e.State == PlaybackState.Playing || e.State == PlaybackState.Paused;
            IsActuallyPlaying = e.State == PlaybackState.Playing;

            switch (e.State)
            {
                case PlaybackState.Playing:
                    PlaybackStatus = $"Playing: {e.FileName}";
                    UpdateStatus($"Playing: {e.FileName}");
                    break;
                case PlaybackState.Paused:
                    PlaybackStatus = $"Paused: {e.FileName}";
                    UpdateStatus($"Paused: {e.FileName}");
                    break;
                case PlaybackState.Stopped:
                    PlaybackStatus = "Stopped";
                    UpdateStatus("Playback stopped.");
                    if (string.IsNullOrEmpty(e.FileName))
                    {
                        CurrentPlayingFile = string.Empty;
                    }

                    break;
            }

            if (!string.IsNullOrEmpty(e.ErrorMessage))
            {
                PlaybackStatus = $"Error: {e.ErrorMessage}";
                UpdateStatus($"Error: {e.ErrorMessage}");
                Debug.WriteLine($"Playback error: {e.ErrorMessage}");
                IsPlaying = false;
                IsActuallyPlaying = false;
            }
        });
    }

    public void StopPlayback()
    {
        if (_player.CurrentPlaybackState == PlaybackState.Playing ||
            _player.CurrentPlaybackState == PlaybackState.Paused)
        {
            _player.StopFile();
        }
    }

    private void PlayFile(AudioFileItem? fileItem)
    {
        if (fileItem == null) return;

        if (!File.Exists(fileItem.FullPath))
        {
            UpdateStatus($"File not found: {fileItem.Name}");
            return;
        }

        string requestedFileName = fileItem.Name;

        if (CurrentPlayingFile == requestedFileName &&
            (_player.CurrentPlaybackState == PlaybackState.Playing ||
             _player.CurrentPlaybackState == PlaybackState.Paused))
        {
            if (_player.CurrentPlaybackState == PlaybackState.Playing)
            {
                _player.PausePlayback();
            }
            else
            {
                _player.ResumePlayback();
            }
        }
        else
        {
            _player.PlayFile(fileItem.FullPath);
        }
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _loadCancellationTokenSource?.Cancel();
                _loadCancellationTokenSource?.Dispose();
                _loadCancellationTokenSource = null;

                _player.PlaybackStatusChanged -= OnPlaybackStatusChanged;
                _player?.Dispose();
            }

            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
