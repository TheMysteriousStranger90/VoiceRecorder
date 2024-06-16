using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VoiceRecorder.ViewModels;

namespace VoiceRecorder.Views;

public partial class SecondWindow : Window
{
    public SecondWindow()
    {
        InitializeComponent();
        var viewModel = new SecondWindowViewModel();
        DataContext = viewModel;
        viewModel.LoadFoldersAndFiles();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}