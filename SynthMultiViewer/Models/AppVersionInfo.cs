namespace HanumanInstitute.SynthMultiViewer.Models;

/// <summary>
/// Version information returned by the Hanuman Institute app-version API.
/// </summary>
/// <param name="LatestVersion">The latest app version available for download.</param>
/// <param name="DownloadUrl">A direct download URL for the current platform, when provided.</param>
public record AppVersionInfo(Version LatestVersion, string? DownloadUrl = null)
{
}
