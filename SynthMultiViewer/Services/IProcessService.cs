namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Starts processes and opens URLs in the system browser.
/// </summary>
public interface IProcessService
{
    /// <summary>
    /// Opens <paramref name="url"/> in the default browser.
    /// </summary>
    void OpenBrowserUrl(string url);
}
