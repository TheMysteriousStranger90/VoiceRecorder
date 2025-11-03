using System.Windows.Input;
using ReactiveUI;

namespace VoiceRecorder.ViewModels;

internal sealed class MainWindowViewModel : ViewModelBase
{
    private string _statusMessage = string.Empty;
    private ViewModelBase _currentView;

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
            this.RaiseAndSetIfChanged(ref _currentView, value);
            if (value is RecordingViewModel recordingViewModel)
            {
                recordingViewModel.StatusChanged += (s, e) => StatusMessage = e.Message;
            }

            if (value is FileExplorerViewModel fileExplorerViewModel)
            {
                fileExplorerViewModel.StatusChanged += (s, e) => StatusMessage = e.Message;
            }
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
}
