namespace HanumanInstitute.SynthMultiViewer.ViewModels;

/// <summary>
/// A recently opened script shown on the empty-canvas start surface.
/// </summary>
public sealed class RecentFileItem(string path)
{
    /// <summary>
    /// Gets the full path used to reopen the script.
    /// </summary>
    public string Path { get; } = path;

    /// <summary>
    /// Gets the file name shown on the start surface.
    /// </summary>
    public string Name { get; } = System.IO.Path.GetFileName(path);
}
