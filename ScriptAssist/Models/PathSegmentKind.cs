namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// How a path segment was written in the buffer.
/// </summary>
public enum PathSegmentKind
{
    /// <summary>
    /// A dotted name.
    /// </summary>
    Name,

    /// <summary>
    /// A name followed by a call.
    /// </summary>
    Call,

    /// <summary>
    /// A name followed by an index or slice.
    /// </summary>
    Index
}
