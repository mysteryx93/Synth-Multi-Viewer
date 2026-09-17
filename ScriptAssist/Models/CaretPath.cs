namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// The expression to the left of the caret and the identifier being typed.
/// </summary>
public sealed class CaretPath
{
    /// <summary>
    /// Gets the UTF-16 start of the identifier being replaced.
    /// </summary>
    public int Start { get; init; }

    /// <summary>
    /// Gets the UTF-16 end of the identifier being replaced.
    /// </summary>
    public int End { get; init; }

    /// <summary>
    /// Gets the identifier text before the caret.
    /// </summary>
    public string Typed { get; init; } = "";

    /// <summary>
    /// Gets segments to the left of the typed identifier, left to right.
    /// </summary>
    public IReadOnlyList<PathSegment> Segments { get; init; } = [];
}
