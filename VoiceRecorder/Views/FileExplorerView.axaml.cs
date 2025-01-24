using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace VoiceRecorder.Views;

public partial class FileExplorerView : UserControl
{
    public FileExplorerView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}