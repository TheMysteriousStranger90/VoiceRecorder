using System.Windows.Input;
using ReactiveUI;
using VoiceRecorder.Models;

namespace VoiceRecorder.ViewModels;

internal sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private string _statusMessage = string.Empty;
    private ViewModelBase _currentView;
    private bool _disposed;

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

    public ICommand ShowRecordingViewCommand { get; }
    public ICommand ShowFileExplorerCommand { get; }

    public MainWindowViewModel()
    {
        ShowRecordingViewCommand = ReactiveCommand.Create(ShowRecordingView);
        ShowFileExplorerCommand = ReactiveCommand.Create(ShowFileExplorer);

        _currentView = new RecordingViewModel();
        ShowRecordingView();
    }

    private void ShowRecordingView()
    {
        CurrentView = new RecordingViewModel();
    }

    private void ShowFileExplorer()
    {
        CurrentView = new FileExplorerViewModel();
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
