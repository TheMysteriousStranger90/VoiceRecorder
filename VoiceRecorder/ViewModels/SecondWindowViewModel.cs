using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using ReactiveUI;

namespace VoiceRecorder.ViewModels;

public class SecondWindowViewModel : ViewModelBase
{
    public ObservableCollection<string> Folders { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> Files { get; } = new ObservableCollection<string>();
    public ReactiveCommand<string, Unit> OpenFileCommand { get; }

    public SecondWindowViewModel()
    {
        OpenFileCommand = ReactiveCommand.Create<string>(OpenFile);
    }

    public void LoadFoldersAndFiles()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VoiceRecorder");
        int pathLength = path.Length + 1;

        
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        foreach (var directory in Directory.GetDirectories(path, "*", SearchOption.AllDirectories))
        {
            Folders.Add(directory.Substring(pathLength));
        }

        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        {
            Files.Add(file.Substring(pathLength));
        }
    }

    private void OpenFile(string file)
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VoiceRecorder",
            file);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}