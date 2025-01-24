using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using VoiceRecorder.Exceptions;
using VoiceRecorder.Filters;
using VoiceRecorder.Models;
using VoiceRecorder.Utils;

namespace VoiceRecorder.ViewModels;

public class RecordingViewModel : ViewModelBase
{
    private readonly AudioRecorder _recorder;
    private readonly AudioDevice _device;
    private readonly DispatcherTimer _timer;
    private TimeSpan _time;
    private string _timerText;

    public List<VoiceFilterViewModel> AvailableFilters { get; }
    public List<string> AvailableDevices { get; }
    public ICommand StartRecordingCommand { get; }
    public ICommand StopRecordingCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public event EventHandler<string> StatusChanged;

    private void UpdateStatus(string message)
    {
        StatusChanged?.Invoke(this, message);
    }

    private bool _isRecording;

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

    private VoiceFilterViewModel _selectedFilterViewModel;

    public VoiceFilterViewModel SelectedFilterViewModel
    {
        get => _selectedFilterViewModel;
        set => this.RaiseAndSetIfChanged(ref _selectedFilterViewModel, value);
    }

    private string _selectedDevice;

    public string SelectedDevice
    {
        get => _selectedDevice;
        set => this.RaiseAndSetIfChanged(ref _selectedDevice, value);
    }

    public RecordingViewModel()
    {
        _recorder = new AudioRecorder();
        _device = new AudioDevice();

        AvailableDevices = _device.GetAvailableDevices();

        AvailableFilters = new List<VoiceFilterViewModel>
        {
            new VoiceFilterViewModel(null, null),
            new VoiceFilterViewModel(null, new EchoFilter()),
            new VoiceFilterViewModel(null, new FlangerFilter()),
            new VoiceFilterViewModel(null, new DistortionFilter())
        };

        StartRecordingCommand = ReactiveCommand.Create(StartRecording);
        StopRecordingCommand = ReactiveCommand.Create(StopRecording);
        OpenFolderCommand = ReactiveCommand.Create(OpenFolder);

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;
        TimerText = "00:00:00";
    }

    private void Timer_Tick(object sender, EventArgs e)
    {
        _time = _time.Add(TimeSpan.FromSeconds(1));
        TimerText = _time.ToString(@"hh\:mm\:ss");
    }

    private void StartRecording()
    {
        try
        {
            if (SelectedDevice == null) return;

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

        catch (UnauthorizedAccessException)
        {
            UpdateStatus("Please enable microphone access in Windows Privacy Settings");
        }
        catch (AudioRecorderException)
        {
            UpdateStatus("Failed to start recording. Please check your microphone.");
        }
        catch (Exception)
        {
            UpdateStatus("An unexpected error occurred");
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
            Process.Start(new ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true,
                Verb = "open"
            });
        }
    }
}