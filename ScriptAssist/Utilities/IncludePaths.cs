namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Builds candidate paths for script includes without reading the disk.
/// </summary>
internal static class IncludePaths
{
    /// <summary>
    /// AviSynth <c>Import</c> specifiers: rooted paths first, then next to the buffer, then plugin roots.
    /// </summary>
    public static IEnumerable<string> AviSynth(string specifier, string? fromPath, IReadOnlyList<string> roots) =>
        Candidates(specifier, fromPath, roots, false);

    /// <summary>
    /// Python module specifiers: <c>name.py</c> and <c>name/__init__.py</c> under the buffer and plugin roots.
    /// </summary>
    public static IEnumerable<string> PythonModule(string specifier, string? fromPath, IReadOnlyList<string> roots) =>
        Candidates(specifier, fromPath, roots, true);

    private static IEnumerable<string> Candidates(string specifier, string? fromPath, IReadOnlyList<string> roots,
        bool pythonModule)
    {
        specifier = specifier.Trim().Trim('"', '\'');
        if (specifier.Length == 0)
        {
            yield break;
        }

        specifier = specifier.Replace('\\', Path.DirectorySeparatorChar);
        if (pythonModule && specifier[0] == '.')
        {
            foreach (var path in RelativePython(specifier, fromPath))
            {
                yield return path;
            }

            yield break;
        }

        if (Path.IsPathRooted(specifier))
        {
            foreach (var path in Expand(specifier, pythonModule))
            {
                yield return path;
            }

            yield break;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var fromDir = fromPath.HasText() ? Path.GetDirectoryName(fromPath) : null;
        foreach (var directory in Roots(fromDir, roots))
        {
            if (!seen.Add(directory))
            {
                continue;
            }

            var combined = pythonModule
                ? Path.Combine(directory, specifier.Replace('.', Path.DirectorySeparatorChar))
                : Path.Combine(directory, specifier);
            foreach (var path in Expand(combined, pythonModule))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string> Roots(string? fromDir, IReadOnlyList<string> roots)
    {
        if (fromDir.HasText())
        {
            yield return fromDir;
        }

        foreach (var root in roots)
        {
            if (root.HasText())
            {
                yield return root;
            }
        }
    }

    private static IEnumerable<string> RelativePython(string specifier, string? fromPath)
    {
        if (!fromPath.HasText())
        {
            yield break;
        }

        var directory = Path.GetDirectoryName(fromPath);
        var dots = 0;
        while (dots < specifier.Length && specifier[dots] == '.')
        {
            dots++;
        }

        for (var i = 1; i < dots && directory.HasText(); i++)
        {
            directory = Path.GetDirectoryName(directory);
        }

        if (!directory.HasText())
        {
            yield break;
        }

        var rest = specifier[dots..].Replace('.', Path.DirectorySeparatorChar);
        if (rest.Length == 0)
        {
            yield return Path.Combine(directory, "__init__.py");
            yield break;
        }

        foreach (var path in Expand(Path.Combine(directory, rest), true))
        {
            yield return path;
        }
    }

    private static IEnumerable<string> Expand(string path, bool pythonModule)
    {
        if (!pythonModule)
        {
            yield return path;
            yield break;
        }

        yield return path + ".py";
        yield return Path.Combine(path, "__init__.py");
    }
}