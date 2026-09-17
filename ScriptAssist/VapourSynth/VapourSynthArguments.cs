namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Reads VapourSynth catalog argument strings.
/// </summary>
public static class VapourSynthArguments
{
    /// <summary>
    /// Gets whether the first argument is a video or audio node.
    /// </summary>
    public static bool TakesNode(Symbol symbol)
    {
        var first = symbol.Parameters is { Length: > 0 } ? symbol.Parameters[0] : null;
        if (!first.HasValue())
        {
            return false;
        }

        return first.Contains(":vnode", StringComparison.Ordinal) || first.Contains(":anode", StringComparison.Ordinal);
    }
}
