using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using VoiceRecorder.ViewModels;

namespace VoiceRecorder.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
