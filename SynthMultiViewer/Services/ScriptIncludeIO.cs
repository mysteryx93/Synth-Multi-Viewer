using HanumanInstitute.ApiAviSynth;
using HanumanInstitute.ApiVapourSynth;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Reads AviSynth and VapourSynth include specifiers from disk.
/// </summary>
internal static class ScriptIncludeIO
{
    /// <summary>
    /// Follows an AviSynth <c>Import</c> specifier.
    /// </summary>
    public static IncludeFile? AviSynth(string specifier, string? fromPath) =>
        ScriptFiles.AviSynth(specifier, fromPath, AvsScript.GetPluginDirectories(), TryRead);

    /// <summary>
    /// Follows a Python module specifier on the script directory, plugin folders, and site-packages.
    /// </summary>
    public static IncludeFile? VapourSynth(string specifier, string? fromPath)
    {
        VsHelper.TryFindLibrary(out var libraryPath);
        var roots = VsHelper.GetPluginDirectories(libraryPath)
            .Concat(VsPathResolver.GetPythonModuleDirectories(libraryPath))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return ScriptFiles.PythonModule(specifier, fromPath, roots, TryRead);
    }

    private static string? TryRead(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}