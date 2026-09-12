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
        VapourSynth = CreateInstall(
            vapourSynthFound, vapourSynthPath, settings.VapourSynthPath,
            VsHelper.GetPluginDirectories(vapourSynthPath));

        var aviSynthFound = AvsScript.TryFindLibrary(out var aviSynthPath);
        AviSynth = CreateInstall(
            aviSynthFound, aviSynthPath, settings.AviSynthPath, AvsScript.GetPluginDirectories(aviSynthPath));
    }

    private static FrameworkInstall CreateInstall(
        bool found, string? libraryPath, string configuredPath, IReadOnlyList<string> pluginDirectories)
    {
        if (found)
        {
            return new FrameworkInstall(FrameworkStatus.Detected, libraryPath, pluginDirectories);
        }

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return new FrameworkInstall(FrameworkStatus.Error, libraryPath, pluginDirectories);
        }

        return new FrameworkInstall(FrameworkStatus.NotFound, libraryPath, pluginDirectories);
    }
}
