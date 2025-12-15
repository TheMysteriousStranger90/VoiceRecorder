using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using VoiceRecorder.Exceptions;
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
    private System.Threading.Timer? _displayTimer;
    private string _timerText = "00:00:00";
    private bool _isRecording;
    private VoiceFilterViewModel _selectedFilterViewModel;
    private string _selectedDevice = string.Empty;
    private string _statusMessage = "Ready";
    private string _filterTooltip = "Select an audio filter";
    private string _deviceTooltip = "Select a recording device";
    private float[] _audioSamples = [];

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

    public float[] AudioSamples
    {
        get => _audioSamples;
        private set => this.RaiseAndSetIfChanged(ref _audioSamples, value);
    }

    public RecordingViewModel(
        IAudioRecorder? recorder = null,
        IAudioDevice? deviceService = null,
        ISettingsService? settingsService = null)
    {
        _recorder = recorder ?? new AudioRecorder();
        _deviceService = deviceService ?? new AudioDevice();
        _settingsService = settingsService ?? new SettingsService();

        _recorder.AudioDataAvailable += OnAudioDataAvailable;
        _recorder.RecordingStarted += OnRecordingStarted;

        AvailableFilters =
        [
            new VoiceFilterViewModel(null),
            new VoiceFilterViewModel(new EchoFilter()),
            new VoiceFilterViewModel(new FlangerFilter()),
            new VoiceFilterViewModel(new DistortionFilter()),
            new VoiceFilterViewModel(new ChorusFilter()),
            new VoiceFilterViewModel(new CompressorFilter())
        ];
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

        RxApp.MainThreadScheduler.Schedule(async () => await LoadDevicesAsync().ConfigureAwait(false));
    }

    private void OnAudioDataAvailable(object? sender, AudioDataEventArgs e)
    {
        Dispatcher.UIThread.Post(() => AudioSamples = e.Samples);
    }

    private async Task LoadDevicesAsync()
    {
        try
        {
            var devices = await _deviceService.GetAvailableDevicesAsync().ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AvailableDevices.Clear();
                foreach (var device in devices)
                {
                    AvailableDevices.Add(device);
                }
            });

            var defaultDeviceName = await _deviceService.GetDefaultDeviceNameAsync().ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!string.IsNullOrEmpty(defaultDeviceName) && AvailableDevices.Contains(defaultDeviceName))
                {
                    SelectedDevice = defaultDeviceName;
                }
                else if (AvailableDevices.Count > 0)
                {
                    SelectedDevice = AvailableDevices[0];
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading devices: {ex.Message}");
            await Dispatcher.UIThread.InvokeAsync(() => StatusMessage = "Failed to load audio devices");
        }
    }

    private async Task<System.Reactive.Unit> InitializeRecorderWithDeviceAsync(string deviceName)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() => StatusMessage = "Selecting microphone...");

            var device = await _deviceService.SelectDeviceAsync(deviceName).ConfigureAwait(false);
            await _recorder.SetDeviceAsync(device).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() => StatusMessage = "Ready to record");
        }
        catch (AudioDeviceException ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() => StatusMessage = ex.Message);
            Debug.WriteLine($"Audio device error: {ex.ErrorType} - {ex.Message}");
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() => StatusMessage = $"Failed to select microphone: {ex.Message}");
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
                settings).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsRecording = true;
                StatusMessage =
                    $"Recording: {Path.GetFileName(filePath)} [{settings.SampleRate}Hz, {settings.BitsPerSample}bit, {settings.Channels}ch]";
            });
        }
        catch (AudioDeviceException ex)
        {
            await HandleRecordingErrorAsync(ex.Message);
        }
        catch (UnauthorizedAccessException)
        {
            await HandleRecordingErrorAsync("Microphone access denied! Please allow microphone access in Windows Privacy Settings.");
        }
        catch (InvalidOperationException ex)
        {
            await HandleRecordingErrorAsync(ex.Message);
        }
        catch (Exception ex)
        {
            await HandleRecordingErrorAsync($"Recording error: {ex.Message}");
            Debug.WriteLine($"Start error: {ex}");
        }
    }

    private async Task HandleRecordingErrorAsync(string message)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            StatusMessage = message;
            IsRecording = false;
            StopDisplayTimer();
            _stopwatch.Stop();
        });
    }

    private async Task StopRecordingAsync()
    {
        try
        {
            var finalDuration = TimerText;

            await _recorder.StopRecordingAsync().ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StopDisplayTimer();
                _stopwatch.Stop();
                IsRecording = false;
                StatusMessage = $"Saved. Duration: {finalDuration}";
            });
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsRecording = false;
                StopDisplayTimer();
                _stopwatch.Stop();
                StatusMessage = "Recording stopped";
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Stop error: {ex}");

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsRecording = false;
                StopDisplayTimer();
                _stopwatch.Stop();
                StatusMessage = $"Error saving recording: {ex.Message}";
            });
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
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = folderPath,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                StatusMessage = $"Cannot open folder: {ex.Message}");
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
        Dispatcher.UIThread.Post(() =>
        {
            _stopwatch.Restart();
            TimerText = "00:00:00";
            StartDisplayTimer();
        });
    }

    private void StartDisplayTimer()
    {
        StopDisplayTimer();

        _displayTimer = new System.Threading.Timer(
            callback: _ => UpdateTimerDisplay(),
            state: null,
            dueTime: TimeSpan.Zero,
            period: TimeSpan.FromMilliseconds(500));
    }

    private void StopDisplayTimer()
    {
        _displayTimer?.Dispose();
        _displayTimer = null;
        _timerSubscription?.Dispose();
        _timerSubscription = null;
    }

    private void UpdateTimerDisplay()
    {
        if (!_stopwatch.IsRunning)
        {
            return;
        }

        var elapsed = _stopwatch.Elapsed;
        var text = elapsed.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

        Dispatcher.UIThread.Post(() =>
        {
            if (IsRecording)
            {
                TimerText = text;
            }
        });
    }

    public void Dispose()
    {
        _recorder.AudioDataAvailable -= OnAudioDataAvailable;
        _recorder.RecordingStarted -= OnRecordingStarted;
        StopDisplayTimer();
        _disposables.Dispose();
        _recorder.Dispose();
        GC.SuppressFinalize(this);
    }
}
