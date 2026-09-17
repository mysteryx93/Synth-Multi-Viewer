using System.Text.RegularExpressions;

namespace HanumanInstitute.ApiAviSynth;

/// <summary>
/// Walks <c>Import</c> files before native evaluation so a cycle cannot hang AviSynth.
/// </summary>
internal static partial class AvsImportGraph
{
    [GeneratedRegex(@"\bImport\s*\(\s*(?:""""""([\s\S]*?)""""""|""([^""]*)"")\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex ImportCall();

    /// <summary>
    /// Throws when an <c>Import</c> re-enters a file already being loaded.
    /// </summary>
    public static void ThrowIfRecursive(string script, string? scriptPath)
    {
        var stack = new List<string>();
        var done = new HashSet<string>(StringComparer.Ordinal);
        if (scriptPath.HasText())
        {
            try
            {
                var root = Path.GetFullPath(scriptPath);
                stack.Add(root);
                done.Add(root);
            }
            catch (ArgumentException)
            {
            }
        }

        Walk(script, scriptPath, stack, done);
    }

    private static void Walk(string script, string? fromPath, List<string> stack, HashSet<string> done)
    {
        foreach (Match match in ImportCall().Matches(script))
        {
            var specifier = match.Groups[1].Length > 0 ? match.Groups[1].Value : match.Groups[2].Value;
            var path = Resolve(specifier, fromPath);
            if (path == null)
            {
                continue;
            }

            if (stack.Contains(path))
            {
                throw new AvsException("AviSynth Import cycle: " + path);
            }

            if (!done.Add(path))
            {
                continue;
            }

            string nested;
            try
            {
                nested = File.ReadAllText(path);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            stack.Add(path);
            Walk(nested, path, stack, done);
            stack.RemoveAt(stack.Count - 1);
        }
    }

    private static string? Resolve(string specifier, string? fromPath)
    {
        specifier = specifier.Trim();
        if (specifier.Length == 0)
        {
            return null;
        }

        try
        {
            var combined = Path.IsPathRooted(specifier) || !fromPath.HasText()
                ? specifier
                : Path.Combine(Path.GetDirectoryName(fromPath) ?? "", specifier);
            var full = Path.GetFullPath(combined);
            return File.Exists(full) ? full : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
