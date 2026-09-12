using HanumanInstitute.SynthMultiViewer.Services;
using HanumanInstitute.MvvmDialogs;

namespace HanumanInstitute.SynthMultiViewer.ViewModels;

/// <summary>
/// Provides application information and the help dialog close command.
/// </summary>
public class HelpViewModel : WorkspaceViewModel, IModalDialogViewModel
{
    /// <summary>
    /// Creates application information from the environment service.
    /// </summary>
    public HelpViewModel(IEnvironmentService environmentService)
    {
        AppVersion = environmentService.AppVersion.ToString(3);
    }

    /// <summary>
    /// Gets the result returned when the help dialog closes.
    /// </summary>
    public bool? DialogResult => true;
    /// <summary>
    /// Gets the application version displayed in the dialog.
    /// </summary>
    public string AppVersion { get; }
}
