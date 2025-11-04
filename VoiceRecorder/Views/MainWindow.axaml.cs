using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Microsoft.Extensions.DependencyInjection;
using VoiceRecorder.Interfaces;
using VoiceRecorder.ViewModels;

namespace VoiceRecorder.Views;

public partial class MainWindow : Window
{
    private readonly IThemeService _themeService;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnWindowClosing;

        _themeService = App.ServiceProvider?.GetRequiredService<IThemeService>()
                        ?? new VoiceRecorder.Services.ThemeService();

        DataContext = new MainWindowViewModel(_themeService);

        _themeService.ThemeChanged += OnThemeChanged;
        ApplyTheme(_themeService.CurrentTheme);
    }

    private void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        ApplyTheme(e.Theme);
    }

    private void ApplyTheme(ThemeVariant theme)
    {
        var styles = this.Styles;
        styles.Clear();

        styles.Add(new StyleInclude(new Uri("resm:Styles?assembly=VoiceRecorder"))
        {
            Source = new Uri("avares://VoiceRecorder/Styles/MainTheme.axaml")
        });

        if (theme == ThemeVariant.Second)
        {
            styles.Add(new StyleInclude(new Uri("resm:Styles?assembly=VoiceRecorder"))
            {
                Source = new Uri("avares://VoiceRecorder/Styles/SecondTheme.axaml")
            });
        }
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.OnWindowClosing();
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
