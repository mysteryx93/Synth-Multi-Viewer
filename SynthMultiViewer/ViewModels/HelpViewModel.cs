using Avalonia.Controls;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.SynthMultiViewer.Services;

namespace HanumanInstitute.SynthMultiViewer.ViewModels;

/// <summary>
/// Provides application information and the help dialog close command.
/// </summary>
public partial class HelpViewModel : WorkspaceViewModel, IModalDialogViewModel
{
    private readonly IEnvironmentService _environment;
    private readonly IAppVersionClient _appVersion;

    /// <summary>
    /// Creates application information and starts a latest-version check.
    /// </summary>
    public HelpViewModel(IEnvironmentService environmentService, IAppVersionClient appVersionClient)
    {
        _environment = environmentService;
        _appVersion = appVersionClient;
        AppVersion = environmentService.AppVersion.ToString(3);
        if (!Design.IsDesignMode)
        {
            _ = CheckForUpdates.Execute().Subscribe();
        }
    }

    /// <summary>
    /// Gets the result returned when the help dialog closes.
    /// </summary>
    public bool? DialogResult => true;

    /// <summary>
    /// Gets the application version displayed in the dialog.
    /// </summary>
    public string AppVersion { get; }

    /// <summary>
    /// Gets the text shown on the check-for-updates link.
    /// </summary>
    [Reactive]
    public partial string CheckForUpdateText { get; set; } = "Checking for updates...";

    /// <summary>
    /// Checks the store API for an application update.
    /// </summary>
    public RxCommandVoid CheckForUpdates => field ??= ReactiveCommand.CreateFromTask(CheckForUpdatesImpl);

    private async Task CheckForUpdatesImpl()
    {
        CheckForUpdateText = "Checking for updates...";
        var version = await _appVersion.QueryVersionAsync();
        if (version != null)
        {
            CheckForUpdateText = version.LatestVersion > _environment.AppVersion ?
                $"v{version.LatestVersion} is available!" :
                "You have the latest version";
        }
    }
}
