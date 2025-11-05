using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Reactive;
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
    private CancellationTokenSource? _playbackCts;

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

        OpenFileCommand = ReactiveCommand.CreateFromTask<AudioFileItem>(OpenFileAsync);
        PlayFileCommand = ReactiveCommand.CreateFromTask<AudioFileItem>(PlayFileAsync);
        DeleteFileCommand = ReactiveCommand.CreateFromTask<AudioFileItem>(DeleteFileAsync);
        RefreshFilesCommand = ReactiveCommand.CreateFromTask(LoadFoldersAndFilesAsync);
        PlayPauseCommand = ReactiveCommand.CreateFromTask(PlayPauseAsync);
        StopCommand = ReactiveCommand.CreateFromTask(StopAsync);

        Task.Run(async () => await LoadFoldersAndFilesAsync().ConfigureAwait(false));
    }

    public async Task LoadFoldersAndFilesAsync()
    {
        _loadCancellationTokenSource?.CancelAsync();
        _loadCancellationTokenSource?.Dispose();
        _loadCancellationTokenSource = new CancellationTokenSource();

        var cancellationToken = _loadCancellationTokenSource.Token;

        UpdateStatus("Loading files...");

        await Dispatcher.UIThread.InvokeAsync(() =>
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
            catch (Exception ex)
            {
                UpdateStatus($"Error creating directory: {ex.Message}");
                Debug.WriteLine($"Error creating directory: {ex.Message}");
            }

            return;
        }

        try
        {
            var (directories, loadedFiles) = await Task.Run(() =>
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                        return (new List<string>(), new List<AudioFileItem>());

                    var dirs = Directory.GetDirectories(basePath, "*", SearchOption.AllDirectories)
                        .Select(d => Path.GetRelativePath(basePath, d))
                        .ToList();

                    if (cancellationToken.IsCancellationRequested)
                        return (dirs, new List<AudioFileItem>());

                    var files = new List<AudioFileItem>();
                    var wavFiles = Directory.GetFiles(basePath, "*.wav", SearchOption.AllDirectories);

                    foreach (var file in wavFiles)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        try
                        {
                            files.Add(new AudioFileItem(file, basePath));
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error loading file info for {file}: {ex.Message}");
                        }
                    }

                    return (dirs, files.OrderByDescending(f => f.DateCreated).ToList());
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in LoadFoldersAndFiles: {ex.Message}");
                    return (new List<string>(), new List<AudioFileItem>());
                }
            }, cancellationToken).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var directory in directories)
                {
                    Folders.Add(directory);
                }

                foreach (var fileItem in loadedFiles)
                {
                    Files.Add(fileItem);
                }

                UpdateStatus(Files.Count != 0 ? $"{Files.Count} file(s) loaded." : "No audio files found.");
            });
        }
        catch (OperationCanceledException)
        {
            UpdateStatus("Loading cancelled.");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error loading files: {ex.Message}");
            Debug.WriteLine($"Error in LoadFoldersAndFiles: {ex.Message}");
        }
    }

    private async Task DeleteFileAsync(AudioFileItem? fileItem)
    {
        if (fileItem == null) return;

        Debug.WriteLine(string.Format(CultureInfo.InvariantCulture, "Attempting to delete: {0}", fileItem.FullPath));

        try
        {
            if (IsPlaying && CurrentPlayingFile == fileItem.Name)
            {
                await StopAsync().ConfigureAwait(false);
            }

            await Task.Run(() =>
            {
                if (!File.Exists(fileItem.FullPath))
                {
                    return false;
                }

                File.Delete(fileItem.FullPath);
                return true;
            }).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Files.Remove(fileItem);
                UpdateStatus($"File '{fileItem.Name}' deleted.");
            });
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error deleting file '{fileItem.Name}': {ex.Message}");
            Debug.WriteLine($"Error deleting file: {ex.Message}");
        }
    }

    private async Task PlayPauseAsync()
    {
        if (IsPlaying)
        {
            await _player.StopFileAsync(_playbackCts?.Token ?? default).ConfigureAwait(false);
        }
        else if (!string.IsNullOrEmpty(CurrentPlayingFile))
        {
            var fileToPlay = Files.FirstOrDefault(f => f.Name == CurrentPlayingFile);
            if (fileToPlay != null)
            {
                await PlayFileAsync(fileToPlay).ConfigureAwait(false);
            }
        }
    }

    private async Task StopAsync()
    {
        _playbackCts?.CancelAsync();
        await _player.StopFileAsync().ConfigureAwait(false);
    }

    private async Task OpenFileAsync(AudioFileItem? fileItem)
    {
        if (fileItem == null) return;

        if (!File.Exists(fileItem.FullPath))
        {
            UpdateStatus($"File not found: {fileItem.Name}");
            return;
        }

        try
        {
            await Task.Run(() => { Process.Start(new ProcessStartInfo(fileItem.FullPath) { UseShellExecute = true }); })
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error opening file: {ex.Message}");
            Debug.WriteLine($"Error opening file: {ex.Message}");
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

    public async Task StopPlaybackAsync()
    {
        if (_player.CurrentPlaybackState == PlaybackState.Playing ||
            _player.CurrentPlaybackState == PlaybackState.Paused)
        {
            await _player.StopFileAsync().ConfigureAwait(false);
        }
    }

    private async Task PlayFileAsync(AudioFileItem? fileItem)
    {
        if (fileItem == null) return;

        if (!File.Exists(fileItem.FullPath))
        {
            UpdateStatus($"File not found: {fileItem.Name}");
            return;
        }

        _playbackCts?.CancelAsync();
        _playbackCts?.Dispose();
        _playbackCts = new CancellationTokenSource();

        string requestedFileName = fileItem.Name;

        try
        {
            if (CurrentPlayingFile == requestedFileName &&
                (_player.CurrentPlaybackState == PlaybackState.Playing ||
                 _player.CurrentPlaybackState == PlaybackState.Paused))
            {
                if (_player.CurrentPlaybackState == PlaybackState.Playing)
                {
                    await _player.PausePlaybackAsync(_playbackCts.Token).ConfigureAwait(false);
                }
                else
                {
                    await _player.ResumePlaybackAsync(_playbackCts.Token).ConfigureAwait(false);
                }
            }
            else
            {
                await _player.PlayFileAsync(fileItem.FullPath, _playbackCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            UpdateStatus("Playback cancelled.");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Playback error: {ex.Message}");
            Debug.WriteLine($"Playback error: {ex.Message}");
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

                _playbackCts?.Cancel();
                _playbackCts?.Dispose();
                _playbackCts = null;

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
