using Avalonia.Controls;
using Avalonia.Controls.Templates;
using VoiceRecorder.ViewModels;

namespace VoiceRecorder;

internal sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null)
            return null;

        var name = data.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
        {
            var control = Activator.CreateInstance(type);
            if (control is Control c)
                return c;
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
