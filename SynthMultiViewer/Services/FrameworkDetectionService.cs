using HanumanInstitute.ApiAviSynth;
using HanumanInstitute.ApiVapourSynth;
using HanumanInstitute.SynthMultiViewer.Models;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Applies configured library and plugin paths and probes native script engines.
/// </summary>
public sealed class FrameworkDetectionService : IFrameworkDetectionService
{
    /// <summary>
    /// Creates a detector and applies paths from saved settings.
    /// </summary>
    public FrameworkDetectionService(ISettingsProvider<AppSettingsData> settings) => Apply(settings.Value);

    /// <inheritdoc />
    public FrameworkInstall VapourSynth { get; private set; } = new(false);

    /// <inheritdoc />
    public FrameworkInstall AviSynth { get; private set; } = new(false);

    /// <inheritdoc />
    public void Apply(AppSettingsData settings)
    {
        VsHelper.SetDllPath(settings.VapourSynthPath);
        VsHelper.SetPluginFolders(
            FolderListText.Parse(settings.VapourSynthPluginFolders), settings.VapourSynthReplacePlugins);
        AvsScript.SetDllPath(settings.AviSynthPath);
        AvsScript.SetPluginFolders(
            FolderListText.Parse(settings.AviSynthPluginFolders), settings.AviSynthReplacePlugins);

        var vapourSynthFound = VsHelper.TryFindLibrary(out var vapourSynthPath);
        var vapourSynthUsable = VsHelper.TryEvaluate(out var vapourSynthError);
        VapourSynth = CreateInstall(
            vapourSynthFound, vapourSynthUsable, vapourSynthPath, settings.VapourSynthPath,
            VsHelper.GetPluginDirectories(vapourSynthPath), vapourSynthError);

        var aviSynthFound = AvsScript.TryFindLibrary(out var aviSynthPath);
        var aviSynthUsable = AvsScript.TryEvaluate(out var aviSynthError);
        AviSynth = CreateInstall(
            aviSynthFound, aviSynthUsable, aviSynthPath, settings.AviSynthPath,
            AvsScript.GetPluginDirectories(aviSynthPath), aviSynthError);
    }

    private static FrameworkInstall CreateInstall(
        bool found, bool usable, string? libraryPath, string configuredPath,
        IReadOnlyList<string> pluginDirectories, string? error)
    {
        if (found && usable)
        {
            return new FrameworkInstall(FrameworkStatus.Detected, libraryPath, pluginDirectories);
        }

        if (found)
        {
            return new FrameworkInstall(
                FrameworkStatus.Error, libraryPath, pluginDirectories,
                FirstMessage(error, "The library could not run a script."));
        }

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return new FrameworkInstall(
                FrameworkStatus.Error, libraryPath, pluginDirectories,
                FirstMessage(error, "Could not load from '" + configuredPath.Trim() + "'."));
        }

        return new FrameworkInstall(FrameworkStatus.NotFound, libraryPath, pluginDirectories);
    }

    private static string FirstMessage(string? error, string fallback) =>
        string.IsNullOrWhiteSpace(error) ? fallback : error;
}
