using System.Windows.Input;
using ReactiveUI;

namespace VoiceRecorder.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private string _statusMessage;
    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    private ViewModelBase _currentView;
    public ViewModelBase CurrentView
    {
        get => _currentView;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentView, value);
            if (value is RecordingViewModel recordingViewModel)
            {
                recordingViewModel.StatusChanged += (s, message) => StatusMessage = message;
            }
        }
    }

    public ICommand ShowRecordingViewCommand { get; }
    public ICommand ShowFileExplorerCommand { get; }

    public MainWindowViewModel()
    {
        ShowRecordingViewCommand = ReactiveCommand.Create(ShowRecordingView);
        ShowFileExplorerCommand = ReactiveCommand.Create(ShowFileExplorer);

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