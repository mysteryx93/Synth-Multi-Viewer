namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Resolves include specifiers to text using a host-supplied reader.
/// </summary>
public static class ScriptFiles
{
    /// <summary>
    /// Returns the first readable candidate for an AviSynth <c>Import</c> specifier.
    /// </summary>
    public static IncludeFile? AviSynth(string specifier, string? fromPath, IReadOnlyList<string> roots,
        Func<string, string?> tryRead) =>
        Open(IncludePaths.AviSynth(specifier, fromPath, roots), tryRead);

    /// <summary>
    /// Returns the first readable candidate for a Python module specifier.
    /// </summary>
    public static IncludeFile? PythonModule(string specifier, string? fromPath, IReadOnlyList<string> roots,
        Func<string, string?> tryRead) =>
        Open(IncludePaths.PythonModule(specifier, fromPath, roots), tryRead);

    private static IncludeFile? Open(IEnumerable<string> candidates, Func<string, string?> tryRead)
    {
        foreach (var candidate in candidates)
        {
            var text = tryRead(candidate);
            if (text != null)
            {
                return new IncludeFile(Path.GetFullPath(candidate), text);
            }
        }

        return null;
    }
}