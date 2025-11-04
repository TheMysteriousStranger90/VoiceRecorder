using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using Avalonia.Threading;
using CSCore.CoreAudioAPI;
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
    private readonly IAudioRecorder _recorder;
    private readonly IAudioDevice _device;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = new();
    private string _timerText = "00:00:00";
    private bool _isRecording;
    private VoiceFilterViewModel _selectedFilterViewModel;
    private string _selectedDevice = string.Empty;
    private string _filterTooltip = "Select an audio filter";
    private string _deviceTooltip = "Select a recording device";
    private bool _disposed;

    public ObservableCollection<VoiceFilterViewModel> AvailableFilters { get; }
    public ObservableCollection<string> AvailableDevices { get; }
    public ICommand StartRecordingCommand { get; }
    public ICommand StopRecordingCommand { get; }
    public ICommand OpenFolderCommand { get; }

    public event EventHandler<StatusChangedEventArgs>? StatusChanged;

    private void UpdateStatus(string message)
    {
        StatusChanged?.Invoke(this, new StatusChangedEventArgs(message));
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

    public RecordingViewModel(IAudioRecorder? recorder = null, IAudioDevice? device = null)
    {
        _recorder = recorder ?? new AudioRecorder();
        _device = device ?? new AudioDevice();

        try
        {
            var devices = _device.GetAvailableDevices();
            AvailableDevices = new ObservableCollection<string>(devices);

            if (AvailableDevices.Count > 0)
            {
                _selectedDevice = AvailableDevices[0];
            }
        }
        catch (CoreAudioAPIException coreEx)
        {
            Debug.WriteLine($"Audio API error loading devices: {coreEx.Message}");
            AvailableDevices = new ObservableCollection<string>();
            UpdateStatus("Failed to load audio devices: Audio API error");
        }
        catch (InvalidOperationException ioEx)
        {
            Debug.WriteLine($"Invalid operation loading devices: {ioEx.Message}");
            AvailableDevices = new ObservableCollection<string>();
            UpdateStatus("Failed to load audio devices: Invalid operation");
        }
        catch (UnauthorizedAccessException uaEx)
        {
            Debug.WriteLine($"Access denied loading devices: {uaEx.Message}");
            AvailableDevices = new ObservableCollection<string>();
            UpdateStatus("Failed to load audio devices: Access denied");
        }

        AvailableFilters = new ObservableCollection<VoiceFilterViewModel>
        {
            new VoiceFilterViewModel(null!, null!),
            new VoiceFilterViewModel(null!, new EchoFilter()),
            new VoiceFilterViewModel(null!, new FlangerFilter()),
            new VoiceFilterViewModel(null!, new DistortionFilter()),
            new VoiceFilterViewModel(null!, new ChorusFilter()),
            new VoiceFilterViewModel(null!, new CompressorFilter())
        };

        _selectedFilterViewModel = AvailableFilters[0];

        var canStartRecording = this.WhenAnyValue(
            x => x.IsRecording,
            x => x.SelectedDevice,
            (isRecording, device) => !isRecording && !string.IsNullOrEmpty(device));

        var canStopRecording = this.WhenAnyValue(x => x.IsRecording);

        StartRecordingCommand = ReactiveCommand.Create(StartRecording, canStartRecording);
        StopRecordingCommand = ReactiveCommand.Create(StopRecording, canStopRecording);
        OpenFolderCommand = ReactiveCommand.Create(OpenFolder);

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;
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

    private void Timer_Tick(object? sender, EventArgs e)
    {
        TimerText = _stopwatch.Elapsed.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
    }

    private void StartRecording()
    {
        if (_disposed)
        {
            UpdateStatus("Cannot start recording: view model is disposed");
            return;
        }

        try
        {
            if (string.IsNullOrEmpty(SelectedDevice))
            {
                UpdateStatus("Please select a device");
                return;
            }

            if (AvailableDevices.Count == 0 || !AvailableDevices.Contains(SelectedDevice))
            {
                UpdateStatus("Selected device is not available");
                return;
            }

            _stopwatch.Restart();
            TimerText = "00:00:00";

            string filePath = AudioFilePathHelper.GenerateAudioFilePath(SelectedDevice);

            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var device = _device.SelectDevice(SelectedDevice);

            _recorder.StartRecording(
                filePath,
                device,
                SelectedFilterViewModel?.FilterStrategy);

            _timer.Start();
            IsRecording = true;

            string filterName = SelectedFilterViewModel?.ToString() ?? "No filter";
            UpdateStatus($"Recording started: {Path.GetFileName(filePath)} | Filter: {filterName}");
        }
        catch (UnauthorizedAccessException ex)
        {
            UpdateStatus("Please enable microphone access in Windows Privacy Settings");
            Debug.WriteLine($"Microphone access denied: {ex.Message}");
            IsRecording = false;
            _timer.Stop();
            _stopwatch.Reset();
        }
        catch (AudioRecorderException ex)
        {
            UpdateStatus("Failed to start recording. Please check your microphone.");
            Debug.WriteLine($"Recording error: {ex.Message}");
            IsRecording = false;
            _timer.Stop();
            _stopwatch.Reset();
        }
        catch (InvalidOperationException ex)
        {
            UpdateStatus($"Recording error: {ex.Message}");
            Debug.WriteLine($"Invalid operation: {ex.Message}");
            IsRecording = false;
            _timer.Stop();
            _stopwatch.Reset();
        }
        catch (IOException ioEx)
        {
            UpdateStatus("Failed to create recording file. Check disk space and permissions.");
            Debug.WriteLine($"I/O error: {ioEx.Message}");
            IsRecording = false;
            _timer.Stop();
            _stopwatch.Reset();
        }
    }

    private void StopRecording()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _recorder.StopRecording();
            _stopwatch.Stop();
            _timer.Stop();
            IsRecording = false;
            UpdateStatus($"Recording saved - Duration: {TimerText}");
        }
        catch (AudioRecorderException ex)
        {
            UpdateStatus("Error stopping recording");
            Debug.WriteLine($"Error stopping recording: {ex.Message}");
            IsRecording = false;
            _timer.Stop();
            _stopwatch.Reset();
        }
    }

    private void OpenFolder()
    {
        string folderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "VoiceRecorder");

        try
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
        }
        catch (Win32Exception ex)
        {
            UpdateStatus($"Failed to open folder: {ex.Message}");
            Debug.WriteLine($"Win32 error opening folder: {ex.Message}");
        }
        catch (IOException ioEx)
        {
            UpdateStatus($"I/O error opening folder: {ioEx.Message}");
            Debug.WriteLine($"I/O error opening folder: {ioEx.Message}");
        }
        catch (UnauthorizedAccessException uaEx)
        {
            UpdateStatus($"Access denied opening folder: {uaEx.Message}");
            Debug.WriteLine($"Access denied opening folder: {uaEx.Message}");
        }
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                if (IsRecording)
                {
                    try
                    {
                        StopRecording();
                    }
                    catch (AudioRecorderException arEx)
                    {
                        Debug.WriteLine($"AudioRecorderException stopping recording during dispose: {arEx.Message}");
                    }
                    catch (InvalidOperationException ioEx)
                    {
                        Debug.WriteLine($"InvalidOperationException stopping recording during dispose: {ioEx.Message}");
                    }
                }

                _timer.Stop();
                _timer.Tick -= Timer_Tick;

                _recorder?.Dispose();
                _device?.Dispose();
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
