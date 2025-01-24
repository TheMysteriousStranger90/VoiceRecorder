using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace VoiceRecorder.Views;

public partial class RecordingView : UserControl
{
    public RecordingView()
    {
        InitializeComponent();
    }
    
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}