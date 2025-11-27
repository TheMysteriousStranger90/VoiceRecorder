using System.Reactive.Disposables;
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
    private readonly ISettingsService _settingsService;
    private bool _isLightTheme;
    private readonly SerialDisposable _statusSubscription = new();

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
                disposableOld.Dispose();
            }

            this.RaiseAndSetIfChanged(ref _currentView, value);

            UpdateStatusSubscription(value);
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
    public ICommand ShowSettingsCommand { get; }
    public ICommand ToggleThemeCommand { get; }

    public MainWindowViewModel(IThemeService? themeService = null, ISettingsService? settingsService = null)
    {
        _themeService = themeService ?? new ThemeService();
        _settingsService = settingsService ?? new SettingsService();

        var settings = _settingsService.LoadSettings();
        _isLightTheme = settings.Theme == ThemeVariant.Second;
        _themeService.SetTheme(settings.Theme);

        ShowRecordingViewCommand = ReactiveCommand.Create(ShowRecordingView);
        ShowFileExplorerCommand = ReactiveCommand.Create(ShowFileExplorer);
        ShowSettingsCommand = ReactiveCommand.Create(ShowSettings);
        ToggleThemeCommand = ReactiveCommand.Create(ToggleTheme);

        _currentView = new RecordingViewModel();
        UpdateStatusSubscription(_currentView);
    }

    private void ToggleTheme()
    {
        IsLightTheme = !IsLightTheme;
        StatusMessage = IsLightTheme ? "Switched to Second Theme" : "Switched to Main Theme";

        var settings = _settingsService.LoadSettings();
        settings.Theme = IsLightTheme ? ThemeVariant.Second : ThemeVariant.Main;
        _settingsService.SaveSettings(settings);
    }

    private void ShowRecordingView()
    {
        CurrentView = new RecordingViewModel();
    }

    private void ShowFileExplorer()
    {
        CurrentView = new FileExplorerViewModel();
    }

    private void ShowSettings()
    {
        CurrentView = new SettingsViewModel(_settingsService, _themeService);
    }

    private void UpdateStatusSubscription(ViewModelBase viewModel)
    {
        if (viewModel is RecordingViewModel recordingVm)
        {
            _statusSubscription.Disposable = recordingVm
                .WhenAnyValue(x => x.StatusMessage)
                .Subscribe(msg => StatusMessage = msg);
        }
        else if (viewModel is FileExplorerViewModel fileExplorerVm)
        {
            fileExplorerVm.StatusChanged += OnStatusChanged;

            _statusSubscription.Disposable = Disposable.Create(() =>
            {
                fileExplorerVm.StatusChanged -= OnStatusChanged;
            });
        }
        else if (viewModel is SettingsViewModel settingsVm)
        {
            settingsVm.StatusChanged += OnStatusChanged;

            _statusSubscription.Disposable = Disposable.Create(() => { settingsVm.StatusChanged -= OnStatusChanged; });
        }
        else
        {
            _statusSubscription.Disposable = Disposable.Empty;
            StatusMessage = "Ready";
        }
    }

    private void OnStatusChanged(object? sender, StatusChangedEventArgs e)
    {
        StatusMessage = e.Message;
    }

    public async Task OnWindowClosingAsync()
    {
        if (_currentView is FileExplorerViewModel fileExplorerViewModel)
        {
            await fileExplorerViewModel.StopPlaybackAsync().ConfigureAwait(false);
        }

        Dispose();
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _statusSubscription.Dispose();

                if (_currentView is IDisposable disposable)
                {
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
