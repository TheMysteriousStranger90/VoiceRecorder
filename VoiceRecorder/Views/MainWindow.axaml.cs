using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using VoiceRecorder.Models;
using VoiceRecorder.ViewModels;

namespace VoiceRecorder.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel viewModel => (MainWindowViewModel)DataContext;
    public AudioRecorder Recorder { get; set; }
    private AudioDevice Device { get; set; }
    private VoiceFilterViewModel FilterViewModel { get; set; }

    public List<string> AvailableDevices => Device.GetAvailableDevices();

    private readonly DispatcherTimer _timer;
    private TimeSpan _time;

    private readonly TextBlock _buttonTextBlock;
    
    public static MainWindow Current { get; private set; }

    public MainWindow()
    {
        InitializeComponent();

        Current = this;
        
        Recorder = new AudioRecorder();
        Device = new AudioDevice();

        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += Timer_Tick;
        _time = TimeSpan.Zero;

        this._buttonTextBlock = this.FindControl<TextBlock>("StopButtonTextBlock") ??
                               throw new Exception("Cannot find text block");
    }

    private void Timer_Tick(object sender, EventArgs e)
    {
        try
        {
            Console.WriteLine("Timer ticked.");

            _time = _time.Add(TimeSpan.FromSeconds(1));
            var timerLabel = this.FindControl<Label>("TimerLabel");
            var progressBar = this.FindControl<ProgressBar>("RecordingProgressBar");

            if (progressBar != null && timerLabel != null)
            {
                timerLabel.Content = _time.ToString(@"hh\:mm\:ss");
                progressBar.Value += 1;
                if (progressBar.Value >= progressBar.Maximum)
                {
                    progressBar.Value = progressBar.Minimum;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.ToString());
        }
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var deviceList = this.FindControl<ListBox>("DeviceList");

            if (deviceList != null && deviceList.SelectedItem != null)
            {
                string selectedDevice = deviceList.SelectedItem.ToString();
                FilterViewModel = viewModel.SelectedFilterViewModel;
                viewModel.StartRecording(selectedDevice, FilterViewModel);
            
                this._buttonTextBlock.Text = $"Recording started.";
                deviceList.IsEnabled = false;
                viewModel.IsSecondWindowActive = false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var deviceList = this.FindControl<ListBox>("DeviceList");

            if (deviceList != null && deviceList.SelectedItem != null)
            {
                string selectedDevice = deviceList.SelectedItem.ToString();
                viewModel.StopRecording(selectedDevice);

                deviceList.IsEnabled = true;
            }

            _timer.Stop();
            this._buttonTextBlock.Text = $"Recording saved.";
            viewModel.IsSecondWindowActive = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "VoiceRecorder");
            if (Directory.Exists(folderPath))
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = folderPath,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            else
            {
                this._buttonTextBlock.Text = "The folder does not exist.";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.ToString());
        }
    }
    
    private void NavigateToMainWindow(object sender, RoutedEventArgs e)
    {
        viewModel.IsMainWindowActive = false;
        MainContent.Content = new MainWindow();
    }

    private void NavigateToSecondWindow(object sender, RoutedEventArgs e)
    {
        if (viewModel.IsSecondWindowActive == true)
        {
            var secondWindow = new SecondWindow();
            secondWindow.Show();
        }
    }
    
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        
        Current = null;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}