namespace HanumanInstitute.SynthMultiViewer.Services;

/// <inheritdoc />
public class AppPathService : IAppPathService
{
    private readonly IEnvironmentService _environment;

    /// <summary>
    /// Creates a path service rooted at the user application-data folder.
    /// </summary>
    public AppPathService(IEnvironmentService environmentService)
    {
        _environment = environmentService;
    }

    /// <inheritdoc />
    public string ConfigFile => field ??= Path.Combine(_environment.ApplicationDataPath, "Hanuman Institute", "SynthMultiViewer.json");
}
