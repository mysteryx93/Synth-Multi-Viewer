using HanumanInstitute.ApiAviSynth;
using HanumanInstitute.ApiVapourSynth;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Reads AviSynth and VapourSynth include specifiers from disk.
/// </summary>
internal static class ScriptIncludeIO
{
    private static readonly Lock VsRootsGate = new();
    private static string[]? _vsRoots;
    private static string? _vsLibrary;

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
        return ScriptFiles.PythonModule(specifier, fromPath, VsRoots(libraryPath), TryRead);
    }

    /// <summary>
    /// Drops cached search roots so the next lookup rediscovers plugin and site-package directories.
    /// </summary>
    public static void Invalidate()
    {
        lock (VsRootsGate)
        {
            _vsRoots = null;
            _vsLibrary = null;
        }
    }

    private static string[] VsRoots(string? libraryPath)
    {
        lock (VsRootsGate)
        {
            if (_vsRoots != null && _vsLibrary == libraryPath)
            {
                return _vsRoots;
            }

            _vsLibrary = libraryPath;
            _vsRoots = VsHelper.GetPluginDirectories(libraryPath)
                .Concat(VsPathResolver.GetPythonModuleDirectories(libraryPath))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return _vsRoots;
        }
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