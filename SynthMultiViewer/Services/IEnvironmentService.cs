namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Provides process information used by view models.
/// </summary>
public interface IEnvironmentService
{
    /// <summary>
    /// Gets process arguments, including the executable path as the first entry.
    /// </summary>
    IReadOnlyList<string> CommandLineArguments { get; }
    /// <summary>
    /// Gets the application assembly version.
    /// </summary>
    Version AppVersion { get; }
    /// <summary>
    /// Gets the per-user application data folder.
    /// </summary>
    string ApplicationDataPath { get; }
    /// <summary>
    /// Gets the current local date and time.
    /// </summary>
    DateTime Now { get; }
}
