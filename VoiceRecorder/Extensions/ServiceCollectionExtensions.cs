using Microsoft.Extensions.DependencyInjection;
using VoiceRecorder.Models;
using VoiceRecorder.ViewModels;
using VoiceRecorder.Views;

namespace VoiceRecorder.Extensions;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<RecordingViewModel>();
        services.AddTransient<FileExplorerViewModel>();

        return services;
    }

    public static IServiceCollection AddViews(this IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        services.AddTransient<RecordingView>();
        services.AddTransient<FileExplorerView>();

        return services;
    }

    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<AudioRecorder>();
        services.AddSingleton<AudioDevice>();
        services.AddTransient<AudioPlayer>();

        return services;
    }
}
