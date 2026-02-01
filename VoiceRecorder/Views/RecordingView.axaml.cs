using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace VoiceRecorder.Views;

[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Window class used across assemblies")]
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
