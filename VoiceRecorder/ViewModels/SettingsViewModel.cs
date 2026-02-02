using System.Collections.ObjectModel;
using System.Windows.Input;
using ReactiveUI;
using VoiceRecorder.Interfaces;
using VoiceRecorder.Models;
using VoiceRecorder.Models.Enums;
using VoiceRecorder.Models.Events;
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
    private OutputFormat _outputFormat;
    private int _mp3Bitrate;
    private bool _enableNoiseReduction;
    private bool _autoGainControl;
    private float _noiseGateThreshold;

    public ObservableCollection<int> AvailableSampleRates { get; }
    public ObservableCollection<int> AvailableBitsPerSample { get; }
    public ObservableCollection<int> AvailableChannels { get; }
    public ObservableCollection<OutputFormat> AvailableOutputFormats { get; }
    public ObservableCollection<int> AvailableMp3Bitrates { get; }

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

    public OutputFormat OutputFormat
    {
        get => _outputFormat;
        set => this.RaiseAndSetIfChanged(ref _outputFormat, value);
    }

    public int Mp3Bitrate
    {
        get => _mp3Bitrate;
        set => this.RaiseAndSetIfChanged(ref _mp3Bitrate, value);
    }

    public bool EnableNoiseReduction
    {
        get => _enableNoiseReduction;
        set => this.RaiseAndSetIfChanged(ref _enableNoiseReduction, value);
    }

    public bool AutoGainControl
    {
        get => _autoGainControl;
        set => this.RaiseAndSetIfChanged(ref _autoGainControl, value);
    }

    public float NoiseGateThreshold
    {
        get => _noiseGateThreshold;
        set => this.RaiseAndSetIfChanged(ref _noiseGateThreshold, value);
    }

    public string NoiseGateThresholdText => $"{NoiseGateThreshold:F0} dB";

    public ICommand SaveSettingsCommand { get; }
    public ICommand ResetSettingsCommand { get; }

    public event EventHandler<StatusChangedEventArgs>? StatusChanged;

    public SettingsViewModel(ISettingsService? settingsService = null, IThemeService? themeService = null)
    {
        _settingsService = settingsService ?? new SettingsService();
        _themeService = themeService ?? new ThemeService();

        AvailableSampleRates =
        [
            8000, 11025, 16000, 22050, 44100, 48000, 96000, 192000
        ];

        AvailableBitsPerSample = [8, 16, 24, 32];
        AvailableChannels = [1, 2];

        AvailableOutputFormats =
        [
            OutputFormat.Wav,
            //OutputFormat.Mp3,
            //OutputFormat.Aac
        ];

        AvailableMp3Bitrates =
        [
            64, 96, 128, 160, 192, 224, 256, 320
        ];

        SaveSettingsCommand = ReactiveCommand.Create(SaveSettings);
        ResetSettingsCommand = ReactiveCommand.Create(ResetSettings);

        this.WhenAnyValue(x => x.NoiseGateThreshold)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(NoiseGateThresholdText)));

        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = _settingsService.LoadSettings();
        SampleRate = settings.SampleRate;
        BitsPerSample = settings.BitsPerSample;
        Channels = settings.Channels;
        IsLightTheme = settings.Theme == ThemeVariant.Second;
        OutputFormat = settings.OutputFormat;
        Mp3Bitrate = settings.Mp3Bitrate;
        EnableNoiseReduction = settings.EnableNoiseReduction;
        AutoGainControl = settings.AutoGainControl;
        NoiseGateThreshold = settings.NoiseGateThreshold;

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
            Theme = IsLightTheme ? ThemeVariant.Second : ThemeVariant.Main,
            OutputFormat = OutputFormat,
            Mp3Bitrate = Mp3Bitrate,
            EnableNoiseReduction = EnableNoiseReduction,
            AutoGainControl = AutoGainControl,
            NoiseGateThreshold = NoiseGateThreshold
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
        OutputFormat = defaultSettings.OutputFormat;
        Mp3Bitrate = defaultSettings.Mp3Bitrate;
        EnableNoiseReduction = defaultSettings.EnableNoiseReduction;
        AutoGainControl = defaultSettings.AutoGainControl;
        NoiseGateThreshold = defaultSettings.NoiseGateThreshold;

        UpdateStatus("Settings reset to default");
    }

    private void UpdateStatus(string message)
    {
        StatusMessage = message;
        StatusChanged?.Invoke(this, new StatusChangedEventArgs(message));
    }
}
