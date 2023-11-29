using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using ReactiveUI;
using VoiceRecorder.ViewModels;

namespace VoiceRecorder.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel viewModel => (MainWindowViewModel)DataContext;
    public AudioRecorder Recorder { get; set; }
    public AudioDevice Device { get; set; }
    public VoiceFilterViewModel FilterViewModel { get; set; }

    public List<string> AvailableDevices => Device.GetAvailableDevices();

    private DispatcherTimer timer;
    private TimeSpan time;

    private TextBlock buttonTextBlock;

    public MainWindow()
    {
        InitializeComponent();

        Recorder = new AudioRecorder();
        Device = new AudioDevice();

        timer = new DispatcherTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += Timer_Tick;
        time = TimeSpan.Zero;

        this.buttonTextBlock = this.FindControl<TextBlock>("StopButtonTextBlock") ??
                               throw new Exception("Cannot find text block");
    }

    private void Timer_Tick(object sender, EventArgs e)
    {
        try
        {
            Console.WriteLine("Timer ticked.");

            time = time.Add(TimeSpan.FromSeconds(1));
            var timerLabel = this.FindControl<Label>("TimerLabel");
            var progressBar = this.FindControl<ProgressBar>("RecordingProgressBar");

            if (progressBar != null && timerLabel != null)
            {
                timerLabel.Content = time.ToString(@"hh\:mm\:ss");
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

                time = TimeSpan.Zero;
                timer.Start();

                this.buttonTextBlock.Text = $"Recording started.";
                deviceList.IsEnabled = false;
            }
            else
            {
                Console.WriteLine("An error occurred");
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

            timer.Stop();
            this.buttonTextBlock.Text = $"Recording saved.";
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
                this.buttonTextBlock.Text = "The folder does not exist.";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.ToString());
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}