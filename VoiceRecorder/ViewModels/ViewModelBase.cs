using System.Diagnostics.CodeAnalysis;
using ReactiveUI;

namespace VoiceRecorder.ViewModels;

[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "Base class for ViewModels used across assemblies")]
public class ViewModelBase : ReactiveObject
{
}
