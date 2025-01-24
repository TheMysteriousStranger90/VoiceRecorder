using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using System.Windows.Input;
using ReactiveUI;
using VoiceRecorder.Models;

namespace VoiceRecorder.ViewModels;

public class FileExplorerViewModel : ViewModelBase, IDisposable
{
    private readonly AudioPlayer _player;
    private string _currentPlayingFile;
    private bool _isPlaying;
    private string _playbackStatus;
    public bool IsPlaying
    {
        get => _isPlaying;
        private set => this.RaiseAndSetIfChanged(ref _isPlaying, value);
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
    public ObservableCollection<string> Folders { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> Files { get; } = new ObservableCollection<string>();
    public ReactiveCommand<string, Unit> OpenFileCommand { get; }
    public ICommand PlayFileCommand { get; }
    public ICommand PlayPauseCommand { get; }
    public ICommand StopCommand { get; }

    public FileExplorerViewModel()
    {
        _player = new AudioPlayer();
        _player.PlaybackStatusChanged += OnPlaybackStatusChanged;
        
        OpenFileCommand = ReactiveCommand.Create<string>(OpenFile);
        PlayFileCommand = ReactiveCommand.Create<string>(PlayFile);
        PlayPauseCommand = ReactiveCommand.Create(PlayPause);
        StopCommand = ReactiveCommand.Create(Stop);
        LoadFoldersAndFiles();
    }

    public void LoadFoldersAndFiles()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VoiceRecorder");
        int pathLength = path.Length + 1;


        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        foreach (var directory in Directory.GetDirectories(path, "*", SearchOption.AllDirectories))
        {
            Folders.Add(directory.Substring(pathLength));
        }

        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        {
            Files.Add(file.Substring(pathLength));
        }
    }
    
    private void PlayPause()
    {
        if (IsPlaying)
        {
            _player.Stop();
        }
        else if (!string.IsNullOrEmpty(CurrentPlayingFile))
        {
            PlayFile(CurrentPlayingFile);
        }
    }

    private void Stop()
    {
        _player.Stop();
        CurrentPlayingFile = null;
        PlaybackStatus = "";
    }

    private void OpenFile(string file)
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VoiceRecorder",
            file);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
    
    private void OnPlaybackStatusChanged(object sender, PlaybackStatusEventArgs e)
    {
        IsPlaying = e.IsPlaying;
        CurrentPlayingFile = e.CurrentFile;
        
        if (!string.IsNullOrEmpty(e.ErrorMessage))
        {
            // Handle error
            Debug.WriteLine($"Playback error: {e.ErrorMessage}");
        }
    }

    private void PlayFile(string fileName)
    {
        try
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "VoiceRecorder",
                fileName);

            if (IsPlaying && CurrentPlayingFile == fileName)
            {
                _player.Stop();
            }
            else
            {
                _player.PlayFile(path);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error playing file: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _player?.Dispose();
    }
}