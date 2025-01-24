using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using VoiceRecorder.Extensions;
using VoiceRecorder.ViewModels;
using VoiceRecorder.Views;

namespace VoiceRecorder;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }
        
        var collection = new ServiceCollection();
        collection.AddCommonViewModels();
        collection.AddCommonWindows();

        base.OnFrameworkInitializationCompleted();
    }
}