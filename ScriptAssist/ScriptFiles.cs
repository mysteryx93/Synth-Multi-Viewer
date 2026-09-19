using HanumanInstitute.ScriptAssist.Services;

namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Resolves include specifiers to readable files under host-supplied roots.
/// </summary>
public static class ScriptFiles
{
    /// <summary>
    /// Returns the first readable candidate for an AviSynth <c>Import</c> specifier.
    /// </summary>
    public static IncludeFile? AviSynth(string specifier, string? fromPath, IReadOnlyList<string> roots,
        IFileSystemService files) =>
        Open(IncludePaths.AviSynth(specifier, fromPath, roots, files.Path), files);

    /// <summary>
    /// Returns the first readable candidate for a Python module specifier.
    /// </summary>
    public static IncludeFile? PythonModule(string specifier, string? fromPath, IReadOnlyList<string> roots,
        IFileSystemService files) =>
        Open(IncludePaths.PythonModule(specifier, fromPath, roots, files.Path), files);

    private static IncludeFile? Open(IEnumerable<string> candidates, IFileSystemService files)
    {
        files.CheckNotNull();
        foreach (var candidate in candidates)
        {
            var text = TryRead(files, candidate);
            if (text != null)
            {
                return new IncludeFile(files.Path.GetFullPath(candidate), text);
            }
        }

        return null;
    }

    private static string? TryRead(IFileSystemService files, string path)
    {
        try
        {
            return files.File.Exists(path) ? files.File.ReadAllText(path) : null;
        }
        catch (System.IO.IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
