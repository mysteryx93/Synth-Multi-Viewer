using HanumanInstitute.SynthMultiViewer.Models;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Queries the Hanuman Institute store for the latest application version.
/// </summary>
public interface IAppVersionClient
{
    /// <summary>
    /// Returns the latest app version available for download, or null when the query fails.
    /// </summary>
    Task<AppVersionInfo?> QueryVersionAsync();
}
