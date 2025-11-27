using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using VoiceRecorder.Controls;
using VoiceRecorder.ViewModels;

namespace VoiceRecorder.Views;

public partial class RecordingView : UserControl
{
    public RecordingView()
    {
        InitializeComponent();
        this.DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is RecordingViewModel viewModel)
        {
            var visualizer = this.FindControl<AudioVisualizerControl>("AudioVisualizer");
            if (visualizer != null)
            {
                viewModel.SetVisualizer(visualizer);
            }
        }
    }
}
