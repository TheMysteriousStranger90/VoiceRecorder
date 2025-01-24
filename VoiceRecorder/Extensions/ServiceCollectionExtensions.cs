using Microsoft.Extensions.DependencyInjection;
using VoiceRecorder.ViewModels;
using VoiceRecorder.Views;

namespace VoiceRecorder.Extensions;

public static class ServiceCollectionExtensions
{
    
    public static void AddCommonViewModels(this IServiceCollection collection)
    {
        collection.AddTransient<MainWindowViewModel>();
        collection.AddTransient<RecordingViewModel>();
        collection.AddTransient<FileExplorerViewModel>();
    }
    
    public static void AddCommonWindows(this IServiceCollection collection)
    {
        collection.AddTransient<MainWindow>();
        collection.AddTransient<RecordingView>();
        collection.AddTransient<FileExplorerView>();
    }
}