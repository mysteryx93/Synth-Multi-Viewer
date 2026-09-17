namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// One identifier in a dotted, called, or indexed expression.
/// </summary>
public sealed class PathSegment
{
    /// <summary>
    /// Gets the identifier text.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets how the identifier was used.
    /// </summary>
    public PathSegmentKind Kind { get; init; }
}
