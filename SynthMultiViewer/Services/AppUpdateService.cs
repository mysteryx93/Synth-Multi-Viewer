using System.ComponentModel;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using HanumanInstitute.SynthMultiViewer.Models;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <inheritdoc />
public class AppUpdateService : IAppUpdateService
{
    /// <summary>
    /// GitHub releases page opened when the store API does not supply a download URL.
    /// </summary>
    public const string ReleasesUrl = "https://github.com/mysteryx93/SynthMultiViewer/releases";

    private readonly ISettingsProvider<AppSettingsData> _settings;
    private readonly IDialogService _dialogService;
    private readonly IEnvironmentService _environment;
    private readonly IAppVersionClient _appVersion;
    private readonly IProcessService _process;

    /// <summary>
    /// Creates a service that checks for updates on the configured interval.
    /// </summary>
    public AppUpdateService(
        ISettingsProvider<AppSettingsData> settings,
        IDialogService dialogService,
        IEnvironmentService environment,
        IAppVersionClient appVersion,
        IProcessService process)
    {
        _settings = settings;
        _dialogService = dialogService;
        _environment = environment;
        _appVersion = appVersion;
        _process = process;
    }

    /// <inheritdoc />
    public async Task CheckForUpdatesAsync(INotifyPropertyChanged owner)
    {
        var interval = _settings.Value.CheckForUpdates;
        var lastCheck = _environment.Now - _settings.Value.LastCheckForUpdate;
        if (interval != UpdateInterval.Never &&
            (lastCheck == null ||
             (interval == UpdateInterval.Daily && lastCheck > TimeSpan.FromDays(1)) ||
             (interval == UpdateInterval.Biweekly && lastCheck > TimeSpan.FromDays(3.5)) ||
             (interval == UpdateInterval.Weekly && lastCheck > TimeSpan.FromDays(7)) ||
             (interval == UpdateInterval.Bimonthly && lastCheck > TimeSpan.FromDays(15)) ||
             (interval == UpdateInterval.Monthly && lastCheck > TimeSpan.FromDays(30))))
        {
            var version = await _appVersion.QueryVersionAsync();
            if (version != null)
            {
                _settings.Value.LastCheckForUpdate = _environment.Now;
                _settings.Save();

                if (version.LatestVersion > _environment.AppVersion)
                {
                    await Task.Delay(1).ConfigureAwait(true);
                    var result = await _dialogService.ShowMessageBoxAsync(owner,
                        $"Version {version.LatestVersion} is available!\n\nWould you like to download it now?".ReplaceLineEndings(),
                        "Update Available!", MessageBoxButton.YesNo, MessageBoxImage.Information);
                    if (result == true)
                    {
                        _process.OpenBrowserUrl(version.DownloadUrl.HasText() ? version.DownloadUrl : ReleasesUrl);
                    }
                }
            }
        }
    }
}
