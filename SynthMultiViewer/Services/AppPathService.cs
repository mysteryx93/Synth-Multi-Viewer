using HanumanInstitute.ScriptAssist.Services;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <inheritdoc />
public class AppPathService : IAppPathService
{
    private readonly IEnvironmentService _environment;
    private readonly IFileSystemService _files;

    /// <summary>
    /// Creates a path service rooted at the user application-data folder.
    /// </summary>
    public AppPathService(IEnvironmentService environmentService, IFileSystemService files)
    {
        _environment = environmentService;
        _files = files.CheckNotNull();
    }

    /// <inheritdoc />
    public string ConfigFile => field ??= _files.Path.Combine(_environment.ApplicationDataPath,
        "Hanuman Institute", "SynthMultiViewer.json");
}
