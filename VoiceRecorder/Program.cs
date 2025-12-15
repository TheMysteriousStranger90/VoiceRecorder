using System.Runtime.Versioning;
using Avalonia;
using Avalonia.ReactiveUI;
using VoiceRecorder.Services;

[assembly: SupportedOSPlatform("windows")]

namespace VoiceRecorder;

internal sealed class Program
{
    public static WindowsInstanceService? SingleInstance { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        SingleInstance = new WindowsInstanceService();

        if (!SingleInstance.TryStart())
        {
            SingleInstance.Dispose();
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            SingleInstance.Dispose();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
