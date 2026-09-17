namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Catalog overloads for a parsed callee.
/// </summary>
public sealed class CallResolution
{
    /// <summary>
    /// Gets matching function symbols.
    /// </summary>
    public required IReadOnlyList<Symbol> Overloads { get; init; }

    /// <summary>
    /// Gets whether the first clip or node argument is bound to the receiver.
    /// </summary>
    public bool ImplicitReceiver { get; init; }
}
