using Avalonia;
using Avalonia.Controls;
using HanumanInstitute.SynthMultiViewer.Models;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Loads and saves application settings from the configured JSON file.
/// </summary>
public sealed class AppSettingsProvider : SettingsProviderBase<AppSettingsData>
{
    private readonly IAppPathService _appPath;

    /// <summary>
    /// Creates a provider and loads the settings file if it exists.
    /// </summary>
    public AppSettingsProvider(ISerializationService serializationService, IAppPathService appPath) :
        base(serializationService)
    {
        _appPath = appPath;
        Load();
    }

    /// <inheritdoc />
    public override string FilePath => _appPath.ConfigFile;

    /// <inheritdoc />
    public override AppSettingsData Load()
    {
        if (Application.Current != null && Design.IsDesignMode) { return GetDefault(); }

        return Load(_appPath.ConfigFile);
    }

    /// <inheritdoc />
    public override void Save() => Save(_appPath.ConfigFile);
}
