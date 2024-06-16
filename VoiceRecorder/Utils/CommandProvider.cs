using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace VoiceRecorder.Utils;

public class CommandProvider
{
    public static readonly AttachedProperty<ICommand> OpenFileCommandProperty =
        AvaloniaProperty.RegisterAttached<CommandProvider, Control, ICommand>("OpenFileCommand");

    public static void SetOpenFileCommand(Control element, ICommand value)
    {
        element.SetValue(OpenFileCommandProperty, value);
    }

    public static ICommand GetOpenFileCommand(Control element)
    {
        return element.GetValue(OpenFileCommandProperty);
    }
}