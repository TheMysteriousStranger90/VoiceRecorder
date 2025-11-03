using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using VoiceRecorder.Exceptions;
using VoiceRecorder.Filters;
using VoiceRecorder.Models;
using VoiceRecorder.Utils;

namespace VoiceRecorder.ViewModels;

internal sealed class RecordingViewModel : ViewModelBase, IDisposable
{
    private readonly AudioRecorder _recorder;
    private readonly AudioDevice _device;
    private readonly DispatcherTimer _timer;
    private TimeSpan _time;
    private string _timerText = "00:00:00";
    private bool _isRecording;
    private VoiceFilterViewModel _selectedFilterViewModel;
    private string _selectedDevice = string.Empty;
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
        set => this.RaiseAndSetIfChanged(ref _isRecording, value);
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

    public RecordingViewModel()
    {
        _recorder = new AudioRecorder();
        _device = new AudioDevice();

        var devices = _device.GetAvailableDevices();
        AvailableDevices = new ObservableCollection<string>(devices);

        AvailableFilters = new ObservableCollection<VoiceFilterViewModel>
        {
            new VoiceFilterViewModel(null!, null!),
            new VoiceFilterViewModel(null!, new EchoFilter()),
            new VoiceFilterViewModel(null!, new FlangerFilter()),
            new VoiceFilterViewModel(null!, new DistortionFilter())
        };

        _selectedFilterViewModel = AvailableFilters[0];

        StartRecordingCommand = ReactiveCommand.Create(StartRecording);
        StopRecordingCommand = ReactiveCommand.Create(StopRecording);
        OpenFolderCommand = ReactiveCommand.Create(OpenFolder);

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _time = _time.Add(TimeSpan.FromSeconds(1));
        TimerText = _time.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
    }

    private void StartRecording()
    {
        try
        {
            if (string.IsNullOrEmpty(SelectedDevice))
            {
                UpdateStatus("Please select a device");
                return;
            }

            _time = TimeSpan.Zero;
            TimerText = "00:00:00";

            string filePath = AudioFilePathHelper.GenerateAudioFilePath(SelectedDevice);
            var device = _device.SelectDevice(SelectedDevice);

            if (SelectedFilterViewModel?.FilterStrategy != null)
            {
                _recorder.StartRecording(filePath, device, SelectedFilterViewModel.FilterStrategy);
            }
            else
            {
                _recorder.StartRecording(filePath, device, null);
            }

            _timer.Start();
            IsRecording = true;
            UpdateStatus("Recording started");
        }
        catch (UnauthorizedAccessException ex)
        {
            UpdateStatus("Please enable microphone access in Windows Privacy Settings");
            Debug.WriteLine($"Microphone access denied: {ex.Message}");
        }
        catch (AudioRecorderException ex)
        {
            UpdateStatus("Failed to start recording. Please check your microphone.");
            Debug.WriteLine($"Recording error: {ex.Message}");
        }
    }

    private void StopRecording()
    {
        _recorder.StopRecording();
        _timer.Stop();
        IsRecording = false;
        UpdateStatus("Recording saved");
    }

    private void OpenFolder()
    {
        string folderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "VoiceRecorder");

        if (Directory.Exists(folderPath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = folderPath,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                UpdateStatus($"Failed to open folder: {ex.Message}");
                Debug.WriteLine($"Error opening folder: {ex.Message}");
            }
        }
        else
        {
            UpdateStatus("Folder does not exist");
        }
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
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
