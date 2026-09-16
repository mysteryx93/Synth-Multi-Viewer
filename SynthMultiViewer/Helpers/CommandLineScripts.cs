namespace HanumanInstitute.SynthMultiViewer.Helpers;

/// <summary>
/// Resolves script paths from process arguments for Open With and CLI launches.
/// </summary>
public static class CommandLineScripts
{
    /// <summary>
    /// Returns existing or well-formed script paths from <paramref name="args"/>, skipping the host executable and flags.
    /// </summary>
    public static IEnumerable<string> FromArguments(IReadOnlyList<string> args)
    {
        foreach (var raw in args.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith('-'))
            {
                continue;
            }

            var path = Resolve(raw.Trim().Trim('"'));
            if (path is null || IsHostBinary(path) || ScriptKindLookup.FromPath(path) is null)
            {
                continue;
            }

            yield return path;
        }
    }

    private static string? Resolve(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            return uri.LocalPath;
        }

        return value.Length == 0 ? null : value;
    }

    private static bool IsHostBinary(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".exe", StringComparison.OrdinalIgnoreCase);
    }
}
