using System.Collections.ObjectModel;
using System.Windows.Input;
using ReactiveUI;
using VoiceRecorder.Interfaces;
using VoiceRecorder.Models;
using VoiceRecorder.Services;

namespace VoiceRecorder.ViewModels;

internal sealed class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private int _sampleRate;
    private int _bitsPerSample;
    private int _channels;
    private bool _isLightTheme;
    private string _statusMessage = string.Empty;

    public ObservableCollection<int> AvailableSampleRates { get; }
    public ObservableCollection<int> AvailableBitsPerSample { get; }
    public ObservableCollection<int> AvailableChannels { get; }

    public int SampleRate
    {
        get => _sampleRate;
        set => this.RaiseAndSetIfChanged(ref _sampleRate, value);
    }

    public int BitsPerSample
    {
        get => _bitsPerSample;
        set => this.RaiseAndSetIfChanged(ref _bitsPerSample, value);
    }

    public int Channels
    {
        get => _channels;
        set => this.RaiseAndSetIfChanged(ref _channels, value);
    }

    public bool IsLightTheme
    {
        get => _isLightTheme;
        set => this.RaiseAndSetIfChanged(ref _isLightTheme, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public ICommand SaveSettingsCommand { get; }
    public ICommand ResetSettingsCommand { get; }

    public event EventHandler<StatusChangedEventArgs>? StatusChanged;

    public SettingsViewModel(ISettingsService? settingsService = null, IThemeService? themeService = null)
    {
        _settingsService = settingsService ?? new SettingsService();
        _themeService = themeService ?? new ThemeService();

        AvailableSampleRates = new ObservableCollection<int>
        {
            8000, 11025, 16000, 22050, 44100, 48000, 96000, 192000
        };

        AvailableBitsPerSample = new ObservableCollection<int> { 8, 16, 24, 32 };
        AvailableChannels = new ObservableCollection<int> { 1, 2 };

        SaveSettingsCommand = ReactiveCommand.Create(SaveSettings);
        ResetSettingsCommand = ReactiveCommand.Create(ResetSettings);

        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = _settingsService.LoadSettings();
        SampleRate = settings.SampleRate;
        BitsPerSample = settings.BitsPerSample;
        Channels = settings.Channels;
        IsLightTheme = settings.Theme == ThemeVariant.Second;

        _themeService.SetTheme(settings.Theme);

        UpdateStatus("Settings loaded");
    }

    private void SaveSettings()
    {
        var settings = new AudioSettings
        {
            SampleRate = SampleRate,
            BitsPerSample = BitsPerSample,
            Channels = Channels,
            Theme = IsLightTheme ? ThemeVariant.Second : ThemeVariant.Main
        };

        if (!settings.IsValid())
        {
            UpdateStatus("Invalid settings values");
            return;
        }

        _settingsService.SaveSettings(settings);
        _themeService.SetTheme(settings.Theme);

        UpdateStatus("Settings saved successfully");
    }

    private void ResetSettings()
    {
        var defaultSettings = new AudioSettings();
        SampleRate = defaultSettings.SampleRate;
        BitsPerSample = defaultSettings.BitsPerSample;
        Channels = defaultSettings.Channels;
        IsLightTheme = defaultSettings.Theme == ThemeVariant.Second;

        UpdateStatus("Settings reset to default");
    }

    private void UpdateStatus(string message)
    {
        StatusMessage = message;
        StatusChanged?.Invoke(this, new StatusChangedEventArgs(message));
    }
}
