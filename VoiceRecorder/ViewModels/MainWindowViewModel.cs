using System.Windows.Input;
using ReactiveUI;
using VoiceRecorder.Interfaces;
using VoiceRecorder.Models;
using VoiceRecorder.Services;

namespace VoiceRecorder.ViewModels;

internal sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private string _statusMessage = string.Empty;
    private ViewModelBase _currentView;
    private bool _disposed;
    private readonly IThemeService _themeService;
    private bool _isLightTheme;

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public ViewModelBase CurrentView
    {
        get => _currentView;
        set
        {
            if (_currentView is IDisposable disposableOld && _currentView != value)
            {
                UnsubscribeFromStatusChanges(_currentView);
                disposableOld.Dispose();
            }

            this.RaiseAndSetIfChanged(ref _currentView, value);
            SubscribeToStatusChanges(value);
        }
    }

    public bool IsLightTheme
    {
        get => _isLightTheme;
        set
        {
            this.RaiseAndSetIfChanged(ref _isLightTheme, value);
            _themeService.SetTheme(value ? ThemeVariant.Second : ThemeVariant.Main);
        }
    }

    public ICommand ShowRecordingViewCommand { get; }
    public ICommand ShowFileExplorerCommand { get; }
    public ICommand ToggleThemeCommand { get; }

    public MainWindowViewModel(IThemeService? themeService = null)
    {
        _themeService = themeService ?? new ThemeService();
        _isLightTheme = _themeService.CurrentTheme == ThemeVariant.Second;

        ShowRecordingViewCommand = ReactiveCommand.Create(ShowRecordingView);
        ShowFileExplorerCommand = ReactiveCommand.Create(ShowFileExplorer);
        ToggleThemeCommand = ReactiveCommand.Create(ToggleTheme);

        _currentView = new RecordingViewModel();
        ShowRecordingView();
    }

    private void ToggleTheme()
    {
        IsLightTheme = !IsLightTheme;
        StatusMessage = IsLightTheme ? "Switched to Second Theme" : "Switched to Main Theme";
    }

    private void ShowRecordingView()
    {
        CurrentView = new RecordingViewModel();
    }

    private void ShowFileExplorer()
    {
        CurrentView = new FileExplorerViewModel();
    }

    public async Task OnWindowClosingAsync()
    {
        if (_currentView is FileExplorerViewModel fileExplorerViewModel)
        {
            await fileExplorerViewModel.StopPlaybackAsync().ConfigureAwait(false);
        }

        Dispose();
    }

    private void SubscribeToStatusChanges(ViewModelBase viewModel)
    {
        switch (viewModel)
        {
            case RecordingViewModel recordingViewModel:
                recordingViewModel.StatusChanged += OnStatusChanged;
                break;
            case FileExplorerViewModel fileExplorerViewModel:
                fileExplorerViewModel.StatusChanged += OnStatusChanged;
                break;
        }
    }

    private void UnsubscribeFromStatusChanges(ViewModelBase viewModel)
    {
        switch (viewModel)
        {
            case RecordingViewModel recordingViewModel:
                recordingViewModel.StatusChanged -= OnStatusChanged;
                break;
            case FileExplorerViewModel fileExplorerViewModel:
                fileExplorerViewModel.StatusChanged -= OnStatusChanged;
                break;
        }
    }

    private void OnStatusChanged(object? sender, StatusChangedEventArgs e)
    {
        StatusMessage = e.Message;
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                if (_currentView is IDisposable disposable)
                {
                    UnsubscribeFromStatusChanges(_currentView);
                    disposable.Dispose();
                }
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
