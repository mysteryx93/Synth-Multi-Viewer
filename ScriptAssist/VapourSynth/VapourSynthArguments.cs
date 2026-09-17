namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Reads VapourSynth catalog argument strings.
/// </summary>
public static class VapourSynthArguments
{
    /// <summary>
    /// Gets whether the first argument is a video or audio node.
    /// </summary>
    public static bool TakesNode(Symbol symbol) => TakesVideo(symbol) || TakesAudio(symbol);

    /// <summary>
    /// Gets whether the first argument is a video node.
    /// </summary>
    public static bool TakesVideo(Symbol symbol) => FirstContains(symbol, ":vnode");

    /// <summary>
    /// Gets whether the first argument is an audio node.
    /// </summary>
    public static bool TakesAudio(Symbol symbol) => FirstContains(symbol, ":anode");

    private static bool FirstContains(Symbol symbol, string token)
    {
        var first = symbol.Parameters is { Length: > 0 } ? symbol.Parameters[0] : null;
        return first.HasValue() && first.Contains(token, StringComparison.Ordinal);
    }
}
