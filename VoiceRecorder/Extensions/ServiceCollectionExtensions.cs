using Microsoft.Extensions.DependencyInjection;
using VoiceRecorder.Interfaces;
using VoiceRecorder.Services;
using VoiceRecorder.ViewModels;
using VoiceRecorder.Views;

namespace VoiceRecorder.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<RecordingViewModel>();
        services.AddTransient<FileExplorerViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services;
    }

    public static IServiceCollection AddViews(this IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();

        return services;
    }

    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddTransient<IAudioDevice, AudioDevice>();
        services.AddTransient<IAudioRecorder, AudioRecorder>();
        services.AddTransient<IAudioPlayer, AudioPlayer>();

        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ISettingsService, SettingsService>();

        return services;
    }
}
