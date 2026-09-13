using HanumanInstitute.SynthMultiViewer.Models;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Reports how a native script engine probe completed.
/// </summary>
public enum FrameworkStatus
{
    /// <summary>
    /// No library was found.
    /// </summary>
    NotFound,

    /// <summary>
    /// A compatible library was loaded.
    /// </summary>
    Detected,

    /// <summary>
    /// A configured path was set but the library could not be loaded, or a found library failed to run a script.
    /// </summary>
    Error
}

/// <summary>
/// Reports whether a native script engine was found and which paths were detected.
/// </summary>
public sealed record FrameworkInstall(
    FrameworkStatus Status,
    string? LibraryPath = null,
    IReadOnlyList<string>? PluginDirectories = null,
    string? Message = null)
{
    /// <summary>
    /// Creates an install report from a found/not-found probe.
    /// </summary>
    public FrameworkInstall(bool found, string? libraryPath = null, IReadOnlyList<string>? pluginDirectories = null)
        : this(found ? FrameworkStatus.Detected : FrameworkStatus.NotFound, libraryPath, pluginDirectories)
    {
    }

    /// <summary>
    /// Gets whether a compatible library was loaded.
    /// </summary>
    public bool Found => Status == FrameworkStatus.Detected;
}

/// <summary>
/// Detects installed VapourSynth and AviSynth libraries.
/// </summary>
public interface IFrameworkDetectionService
{
    /// <summary>
    /// Gets whether VapourSynth can be loaded.
    /// </summary>
    FrameworkInstall VapourSynth { get; }

    /// <summary>
    /// Gets whether AviSynth can be loaded.
    /// </summary>
    FrameworkInstall AviSynth { get; }

    /// <summary>
    /// Applies library and plugin paths from settings and probes again.
    /// </summary>
    void Apply(AppSettingsData settings);
}
