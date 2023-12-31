﻿using System;
using System.Collections.Generic;
using System.IO;
using ReactiveUI;
using VoiceRecorder.Filters;

namespace VoiceRecorder.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public bool IsFilterApplied { get; set; }
        public AudioRecorder Recorder { get; set; }
        public AudioDevice Device { get; set; }

        public List<VoiceFilterViewModel> AvailableFilters { get; set; }
        public VoiceFilterViewModel SelectedFilterViewModel { get; set; }

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
            set { this.RaiseAndSetIfChanged(ref _isRecording, value); }
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
                new VoiceFilterViewModel(null, new ChorusFilter()),
                new VoiceFilterViewModel(null, new CompressorFilter())
            };
        }

        public void StartRecording(string deviceName, VoiceFilterViewModel filterViewModel)
        {
            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "VoiceRecorder");
            Directory.CreateDirectory(defaultPath);

            string deviceFolderPath = Path.Combine(defaultPath, deviceName);
            Directory.CreateDirectory(deviceFolderPath);

            string fileName = $"output_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
            string filePath = Path.Combine(deviceFolderPath, fileName);

            var device = Device.SelectDevice(deviceName);

            if (filterViewModel != null && filterViewModel.filterStrategy != null)
            {
                Recorder.StartRecording(filePath, device, filterViewModel.filterStrategy);
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