using System.Reflection;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Reads process arguments and the application assembly version.
/// </summary>
public class EnvironmentService : IEnvironmentService
{
    /// <inheritdoc />
    public IReadOnlyList<string> CommandLineArguments { get; } = Environment.GetCommandLineArgs();

    /// <inheritdoc />
    public Version AppVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 10, 0);

    /// <inheritdoc />
    public string ApplicationDataPath =>
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
}
