namespace HanumanInstitute.SynthMultiViewer.Models;

/// <summary>
/// Contains the application settings.
/// </summary>
public class AppSettingsData
{
    /// <summary>
    /// Gets or sets the visual theme: Light or Dark.
    /// </summary>
    public AppTheme Theme { get; set; } = AppTheme.Light;

    /// <summary>
    /// Gets or sets an optional VapourSynth script-library file or directory.
    /// </summary>
    public string VapourSynthPath { get; set; } = "";

    /// <summary>
    /// Gets or sets extra VapourSynth plugin folders, separated by semicolons or newlines.
    /// </summary>
    public string VapourSynthPluginFolders { get; set; } = "";

    /// <summary>
    /// Gets or sets whether auto-detected VapourSynth plugin folders are skipped.
    /// </summary>
    public bool VapourSynthReplacePlugins { get; set; }

    /// <summary>
    /// Gets or sets the VapourSynth worker count when multi-threading is enabled; zero uses the processor count.
    /// </summary>
    public int VapourSynthThreads { get; set; }

    /// <summary>
    /// Gets or sets an optional AviSynth library file or directory.
    /// </summary>
    public string AviSynthPath { get; set; } = "";

    /// <summary>
    /// Gets or sets extra AviSynth plugin folders, separated by semicolons or newlines.
    /// </summary>
    public string AviSynthPluginFolders { get; set; } = "";

    /// <summary>
    /// Gets or sets whether auto-detected AviSynth plugin folders are skipped.
    /// </summary>
    public bool AviSynthReplacePlugins { get; set; }
}
