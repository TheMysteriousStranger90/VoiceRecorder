using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using VoiceRecorder.Interfaces;
using VoiceRecorder.ViewModels;

namespace VoiceRecorder.Views;

public partial class MainWindow : Window
{
    private readonly IThemeService _themeService;
    private bool _isClosing;

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

        styles.Add(new StyleInclude(new Uri("resm:Styles?assembly=AzioVoiceRecorder"))
        {
            Source = new Uri("avares://AzioVoiceRecorder/Styles/MainTheme.axaml")
        });

        if (theme == ThemeVariant.Second)
        {
            styles.Add(new StyleInclude(new Uri("resm:Styles?assembly=AzioVoiceRecorder"))
            {
                Source = new Uri("avares://AzioVoiceRecorder/Styles/SecondTheme.axaml")
            });
        }
    }

    private async void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_isClosing)
        {
            return;
        }

        if (DataContext is MainWindowViewModel viewModel)
        {
            e.Cancel = true;
            _isClosing = true;

            try
            {
                _themeService.ThemeChanged -= OnThemeChanged;

                await viewModel.OnWindowClosingAsync().ConfigureAwait(false);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Closing -= OnWindowClosing;
                    DataContext = null;
                    Close();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during window closing: {ex.Message}");
                _isClosing = false;
            }
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
