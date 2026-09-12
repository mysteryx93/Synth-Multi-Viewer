namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Resolves application storage paths.
/// </summary>
public interface IAppPathService
{
    /// <summary>
    /// Gets the JSON settings file path.
    /// </summary>
    string ConfigFile { get; }
}
