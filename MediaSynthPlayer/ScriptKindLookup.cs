namespace HanumanInstitute.MediaSynthUI;

/// <summary>
/// Infers the script engine from a file path.
/// </summary>
public static class ScriptKindLookup
{
    /// <summary>
    /// Gets VapourSynth script extensions, including the leading dot.
    /// </summary>
    public static IReadOnlyList<string> VapourSynthExtensions { get; } = [".vpy"];

    /// <summary>
    /// Gets AviSynth script extensions, including the leading dot.
    /// </summary>
    public static IReadOnlyList<string> AviSynthExtensions { get; } = [".avs", ".avsi"];

    /// <summary>
    /// Returns the script engine for a known extension, or <see langword="null"/> when the path is not a script.
    /// </summary>
    public static ScriptKind? FromPath(string path)
    {
        var extension = Path.GetExtension(path.CheckNotNullOrEmpty());
        if (AviSynthExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return ScriptKind.AviSynth;
        }

        if (VapourSynthExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return ScriptKind.VapourSynth;
        }

        return null;
    }

    /// <summary>
    /// Returns file-dialog extensions for <paramref name="kind"/>, without a leading dot.
    /// </summary>
    public static IReadOnlyList<string> FileFilterExtensions(ScriptKind kind)
    {
        kind.CheckEnumValid();
        return [.. Extensions(kind).Select(static extension => extension.TrimStart('.'))];
    }

    /// <summary>
    /// Returns the default save extension for <paramref name="kind"/>, including the leading dot.
    /// </summary>
    public static string DefaultExtension(ScriptKind kind)
    {
        kind.CheckEnumValid();
        return Extensions(kind)[0];
    }

    private static IReadOnlyList<string> Extensions(ScriptKind kind) =>
        kind == ScriptKind.AviSynth ? AviSynthExtensions : VapourSynthExtensions;
}
