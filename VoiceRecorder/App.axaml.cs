using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using VoiceRecorder.Extensions;
using VoiceRecorder.Views;

namespace VoiceRecorder;

internal sealed class App : Application
{
    public static IServiceProvider? ServiceProvider { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();

        services.AddViewModels();
        services.AddViews();
        services.AddServices();

        ServiceProvider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            desktop.Exit += OnExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        try
        {
            if (sender is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow?.Close();
            }

            if (ServiceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during exit: {ex.Message}");
        }
    }
}
