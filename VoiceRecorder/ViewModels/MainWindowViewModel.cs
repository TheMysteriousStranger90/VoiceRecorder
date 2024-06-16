using System.Collections.Generic;
using ReactiveUI;
using VoiceRecorder.Filters;
using VoiceRecorder.Models;
using VoiceRecorder.Utils;

namespace VoiceRecorder.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public bool IsFilterApplied { get; set; }
        private AudioRecorder Recorder { get; set; }
        private AudioDevice Device { get; set; }

        public List<VoiceFilterViewModel> AvailableFilters { get; set; }
        public VoiceFilterViewModel SelectedFilterViewModel { get; set; }

        public bool IsMainWindowActive { get; set; } = false;
        public bool IsSecondWindowActive { get; set; } = true;

        private List<string> _availableDevices;

        public List<string> AvailableDevices
        {
            get => _availableDevices;
            set => this.RaiseAndSetIfChanged(ref _availableDevices, value);
        }

        private string _selectedDevice;

        public string SelectedDevice
        {
            get => _selectedDevice;
            set => this.RaiseAndSetIfChanged(ref _selectedDevice, value);
        }

        private bool _isRecording;

        public bool IsRecording
        {
            get { return _isRecording; }
            set
            {
                this.RaiseAndSetIfChanged(ref _isRecording, value);
                IsSecondWindowActive = !value;
            }
        }

        public MainWindowViewModel()
        {
            Recorder = new AudioRecorder();
            Device = new AudioDevice();

            AvailableDevices = Device.GetAvailableDevices();

            AvailableFilters = new List<VoiceFilterViewModel>
            {
                new VoiceFilterViewModel(null, null),
                new VoiceFilterViewModel(null, new EchoFilter()),
                new VoiceFilterViewModel(null, new FlangerFilter()),
                new VoiceFilterViewModel(null, new DistortionFilter())
            };

            SelectedFilterViewModel = AvailableFilters[0];
        }

        public void StartRecording(string deviceName, VoiceFilterViewModel filterViewModel)
        {
            string filePath = AudioFilePathHelper.GenerateAudioFilePath(deviceName);
            var device = Device.SelectDevice(deviceName);

            if (filterViewModel != null && filterViewModel.FilterStrategy != null)
            {
                Recorder.StartRecording(filePath, device, filterViewModel.FilterStrategy);
            }
            else
            {
                Recorder.StartRecording(filePath, device, null);
            }

            IsRecording = true;

            if (IsFilterApplied && SelectedFilterViewModel != null)
            {
                ApplyFilterCommand();
            }
        }

        public void StopRecording(string deviceName)
        {
            Recorder.StopRecording();
            IsRecording = false;

            if (IsFilterApplied && SelectedFilterViewModel != null)
            {
                ApplyFilterCommand();
            }
        }

        public void ApplyFilterCommand()
        {
            if (SelectedFilterViewModel != null)
            {
                SelectedFilterViewModel.ApplyFilter();
                Recorder.UpdateSource(Recorder.CaptureSource);
            }
        }
    }
}