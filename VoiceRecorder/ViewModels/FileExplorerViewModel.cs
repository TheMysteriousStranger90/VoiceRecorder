using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using VoiceRecorder.Models;

namespace VoiceRecorder.ViewModels;

internal sealed class FileExplorerViewModel : ViewModelBase, IDisposable
{
    private readonly AudioPlayer _player;
    private string _currentPlayingFile = string.Empty;
    private bool _isPlaying;
    private bool _isActuallyPlaying;
    private string _playbackStatus = string.Empty;
    private float _volume = 1.0f;
    private bool _disposed;

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

    public FileExplorerViewModel()
    {
        _player = new AudioPlayer();
        _player.PlaybackStatusChanged += OnPlaybackStatusChanged;

        OpenFileCommand = ReactiveCommand.Create<AudioFileItem>(OpenFile);
        PlayFileCommand = ReactiveCommand.Create<AudioFileItem>(PlayFile);
        DeleteFileCommand = ReactiveCommand.CreateFromTask<AudioFileItem>(DeleteFileAsync);
        RefreshFilesCommand = ReactiveCommand.Create(LoadFoldersAndFiles);
        PlayPauseCommand = ReactiveCommand.Create(PlayPause);
        StopCommand = ReactiveCommand.Create(Stop);
        LoadFoldersAndFiles();
    }

    public void LoadFoldersAndFiles()
    {
        UpdateStatus("Loading files...");
        Files.Clear();
        Folders.Clear();

        string basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "VoiceRecorder");

        if (!Directory.Exists(basePath))
        {
            Directory.CreateDirectory(basePath);
            UpdateStatus("Created VoiceRecorder directory.");
            return;
        }

        var directories = Directory.GetDirectories(basePath, "*", SearchOption.AllDirectories);
        foreach (var directory in directories)
        {
            Folders.Add(directory.Substring(basePath.Length + 1));
        }

        Observable.Start(() =>
            {
                var loadedFiles = new System.Collections.Generic.List<AudioFileItem>();
                var files = Directory.GetFiles(basePath, "*.wav", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    try
                    {
                        loadedFiles.Add(new AudioFileItem(file, basePath));
                    }
                    catch (ArgumentException ex)
                    {
                        Debug.WriteLine($"Error loading file info for {file}: {ex.Message}");
                    }
                    catch (IOException ex)
                    {
                        Debug.WriteLine($"IO error loading file info for {file}: {ex.Message}");
                    }
                }
                return loadedFiles.OrderBy(f => f.DateCreated).ToList();
            }, RxApp.TaskpoolScheduler)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(loadedFiles =>
            {
                foreach (var fileItem in loadedFiles)
                {
                    Files.Add(fileItem);
                }

                UpdateStatus(Files.Count != 0 ? "Files loaded." : "No audio files found.");
            }, ex =>
            {
                UpdateStatus($"Error loading files: {ex.Message}");
                Debug.WriteLine($"Error in LoadFoldersAndFiles observable: {ex.Message}");
            });
    }

    private async Task DeleteFileAsync(AudioFileItem? fileItem)
    {
        if (fileItem == null) return;

        Debug.WriteLine(string.Format(CultureInfo.InvariantCulture, "Attempting to delete: {0}", fileItem.FullPath));

        try
        {
            if (IsPlaying && CurrentPlayingFile == fileItem.Name)
            {
                Stop();
            }

            File.Delete(fileItem.FullPath);
            Files.Remove(fileItem);
            UpdateStatus($"File '{fileItem.Name}' deleted.");
        }
        catch (IOException ex)
        {
            UpdateStatus($"Error deleting file '{fileItem.Name}': {ex.Message}");
            Debug.WriteLine($"Error deleting file: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            UpdateStatus($"Access denied deleting file '{fileItem.Name}': {ex.Message}");
            Debug.WriteLine($"Access denied deleting file: {ex.Message}");
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private void PlayPause()
    {
        if (IsPlaying)
        {
            _player.Stop();
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
        _player.Stop();
    }

    private void OpenFile(AudioFileItem? fileItem)
    {
        if (fileItem == null) return;
        try
        {
            Process.Start(new ProcessStartInfo(fileItem.FullPath) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            UpdateStatus($"Error opening file: {ex.Message}");
            Debug.WriteLine($"Error opening file: {ex.Message}");
        }
        catch (IOException ex)
        {
            UpdateStatus($"IO error opening file: {ex.Message}");
            Debug.WriteLine($"IO error opening file: {ex.Message}");
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
            }
        });
    }

    private void PlayFile(AudioFileItem? fileItem)
    {
        if (fileItem == null) return;

        string requestedFileName = fileItem.Name;

        if (CurrentPlayingFile == requestedFileName &&
            (_player.CurrentPlaybackState == PlaybackState.Playing || _player.CurrentPlaybackState == PlaybackState.Paused))
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
