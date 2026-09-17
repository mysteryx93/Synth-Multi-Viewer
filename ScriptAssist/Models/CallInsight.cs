namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Overloads for the enclosing call and its zero-based active argument.
/// </summary>
public sealed record CallInsight(
    IReadOnlyList<Symbol> Overloads,
    int ActiveParameter,
    bool ImplicitClip,
    bool InArgumentValue = false,
    bool InNestedDelimiter = false,
    IReadOnlySet<string>? UsedArgumentNames = null);
