using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using VoiceRecorder.Controls;
using VoiceRecorder.Filters;
using VoiceRecorder.Interfaces;
using VoiceRecorder.Models;
using VoiceRecorder.Services;
using VoiceRecorder.Utils;

namespace VoiceRecorder.ViewModels;

internal sealed class RecordingViewModel : ViewModelBase, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly IAudioRecorder _recorder;
    private readonly IAudioDevice _deviceService;
    private readonly Stopwatch _stopwatch = new();
    private readonly CompositeDisposable _disposables = new();
    private IDisposable? _timerSubscription;
    private string _timerText = "00:00:00";
    private bool _isRecording;
    private VoiceFilterViewModel _selectedFilterViewModel;
    private string _selectedDevice = string.Empty;
    private string _statusMessage = "Ready";
    private string _filterTooltip = "Select an audio filter";
    private string _deviceTooltip = "Select a recording device";
    private AudioVisualizerControl? _visualizer;

    public ObservableCollection<VoiceFilterViewModel> AvailableFilters { get; }
    public ObservableCollection<string> AvailableDevices { get; } = new();

    public ICommand StartRecordingCommand { get; }
    public ICommand StopRecordingCommand { get; }
    public ICommand OpenFolderCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public bool IsRecording
    {
        get => _isRecording;
        set
        {
            this.RaiseAndSetIfChanged(ref _isRecording, value);
            UpdateTooltips();
        }
    }

    public string TimerText
    {
        get => _timerText;
        set => this.RaiseAndSetIfChanged(ref _timerText, value);
    }

    public VoiceFilterViewModel SelectedFilterViewModel
    {
        get => _selectedFilterViewModel;
        set => this.RaiseAndSetIfChanged(ref _selectedFilterViewModel, value);
    }

    public string SelectedDevice
    {
        get => _selectedDevice;
        set => this.RaiseAndSetIfChanged(ref _selectedDevice, value);
    }

    public string FilterTooltip
    {
        get => _filterTooltip;
        private set => this.RaiseAndSetIfChanged(ref _filterTooltip, value);
    }

    public string DeviceTooltip
    {
        get => _deviceTooltip;
        private set => this.RaiseAndSetIfChanged(ref _deviceTooltip, value);
    }

    public RecordingViewModel(IAudioRecorder? recorder = null, IAudioDevice? deviceService = null,
        ISettingsService? settingsService = null)
    {
        _recorder = recorder ?? new AudioRecorder();
        _deviceService = deviceService ?? new AudioDevice();
        _settingsService = settingsService ?? new SettingsService();

        _recorder.AudioDataAvailable += OnAudioDataAvailable;
        _recorder.RecordingStarted += OnRecordingStarted;

        AvailableFilters = new ObservableCollection<VoiceFilterViewModel>
        {
            new VoiceFilterViewModel(null),
            new VoiceFilterViewModel(new EchoFilter()),
            new VoiceFilterViewModel(new FlangerFilter()),
            new VoiceFilterViewModel(new DistortionFilter()),
            new VoiceFilterViewModel(new ChorusFilter()),
            new VoiceFilterViewModel(new CompressorFilter()),
        };
        _selectedFilterViewModel = AvailableFilters[0];

        var canStartRecording = this.WhenAnyValue(
            x => x.IsRecording,
            x => x.SelectedDevice,
            (isRecording, device) => !isRecording && !string.IsNullOrEmpty(device));

        var canStopRecording = this.WhenAnyValue(x => x.IsRecording);

        StartRecordingCommand = ReactiveCommand.CreateFromTask(StartRecordingAsync, canStartRecording);
        StopRecordingCommand = ReactiveCommand.CreateFromTask(StopRecordingAsync, canStopRecording);
        OpenFolderCommand = ReactiveCommand.CreateFromTask(OpenFolderAsync);

        this.WhenAnyValue(x => x.SelectedDevice)
            .Where(device => !string.IsNullOrEmpty(device))
            .DistinctUntilChanged()
            .SelectMany(InitializeRecorderWithDeviceAsync)
            .Subscribe()
            .DisposeWith(_disposables);

        RxApp.MainThreadScheduler.Schedule(async () => await LoadDevicesAsync());
    }

    public void SetVisualizer(AudioVisualizerControl visualizer)
    {
        _visualizer = visualizer;
    }

    private void OnAudioDataAvailable(object? sender, AudioDataEventArgs e)
    {
        _visualizer?.UpdateAudioData(e.Samples);
    }

    private async Task LoadDevicesAsync()
    {
        try
        {
            var devices = await _deviceService.GetAvailableDevicesAsync();

            AvailableDevices.Clear();
            foreach (var device in devices)
            {
                AvailableDevices.Add(device);
            }

            var defaultDeviceName = await _deviceService.GetDefaultDeviceNameAsync();

            if (!string.IsNullOrEmpty(defaultDeviceName) && AvailableDevices.Contains(defaultDeviceName))
            {
                SelectedDevice = defaultDeviceName;
            }
            else if (AvailableDevices.Count > 0)
            {
                SelectedDevice = AvailableDevices[0];
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading devices: {ex.Message}");
            StatusMessage = "Failed to load audio devices";
        }
    }

    private async Task<System.Reactive.Unit> InitializeRecorderWithDeviceAsync(string deviceName)
    {
        try
        {
            StatusMessage = "Initializing microphone...";
            var device = await _deviceService.SelectDeviceAsync(deviceName);

            await _recorder.SetDeviceAsync(device);

            StatusMessage = "Ready to record";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Device error: {ex.Message}";
            Debug.WriteLine($"Init error: {ex}");
        }

        return System.Reactive.Unit.Default;
    }

    private async Task StartRecordingAsync()
    {
        try
        {
            string filePath = AudioFilePathHelper.GenerateAudioFilePath(SelectedDevice);
            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var settings = _settingsService.LoadSettings();

            await _recorder.StartRecordingAsync(
                filePath,
                SelectedFilterViewModel?.FilterStrategy,
                settings);

            IsRecording = true;
            StatusMessage =
                $"Recording: {Path.GetFileName(filePath)} [{settings.SampleRate}Hz, {settings.BitsPerSample}bit, {settings.Channels}ch]";
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "Microphone access denied!";
            IsRecording = false;

            _stopwatch.Stop();
            _timerSubscription?.Dispose();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            IsRecording = false;
            Debug.WriteLine($"Start error: {ex}");

            _stopwatch.Stop();
            _timerSubscription?.Dispose();
        }
    }

    private async Task StopRecordingAsync()
    {
        try
        {
            await _recorder.StopRecordingAsync();

            _stopwatch.Stop();
            _timerSubscription?.Dispose();

            IsRecording = false;
            StatusMessage = $"Saved. Duration: {TimerText}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Error saving recording";
            Debug.WriteLine($"Stop error: {ex}");
        }
    }

    private async Task OpenFolderAsync()
    {
        string folderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "AzioVoiceRecorder");

        try
        {
            await Task.Run(() =>
            {
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                Process.Start(new ProcessStartInfo
                {
                    FileName = folderPath,
                    UseShellExecute = true,
                    Verb = "open"
                });
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Cannot open folder: {ex.Message}";
        }
    }

    private void UpdateTooltips()
    {
        if (_isRecording)
        {
            FilterTooltip = "Cannot change filter during recording";
            DeviceTooltip = "Cannot change device during recording";
        }
        else
        {
            FilterTooltip = "Select an audio filter";
            DeviceTooltip = "Select a recording device";
        }
    }

    private void OnRecordingStarted(object? sender, EventArgs e)
    {
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            _stopwatch.Restart();
            TimerText = "00:00:00";

            _timerSubscription?.Dispose();
            _timerSubscription = Observable.Interval(TimeSpan.FromSeconds(1))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    TimerText = _stopwatch.Elapsed.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
                });
        });
    }

    public void Dispose()
    {
        _recorder.AudioDataAvailable -= OnAudioDataAvailable;
        _recorder.RecordingStarted -= OnRecordingStarted;
        _timerSubscription?.Dispose();
        _disposables.Dispose();
        _recorder.Dispose();
        GC.SuppressFinalize(this);
    }
}
