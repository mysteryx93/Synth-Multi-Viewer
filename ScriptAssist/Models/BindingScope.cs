namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// A function body and the names visible only inside it.
/// </summary>
public sealed class BindingScope
{
    /// <summary>
    /// Gets the inclusive start offset of the <c>function</c> header.
    /// </summary>
    public int Start { get; init; }

    /// <summary>
    /// Gets the inclusive end offset of the function (<c>}</c>, or the buffer end if unclosed).
    /// </summary>
    public int End { get; init; }

    /// <summary>
    /// Gets the function name.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Gets the exclusive end of the parameter list (the <c>{</c> index, or just after <c>)</c>).
    /// </summary>
    public int HeaderEnd { get; init; }

    /// <summary>
    /// Gets locals and parameters assigned in this function.
    /// </summary>
    public IReadOnlyDictionary<string, TypeRef> Names { get; init; } =
        new Dictionary<string, TypeRef>(StringComparer.Ordinal);

    /// <summary>
    /// Gets the function's raw parameter strings.
    /// </summary>
    public IReadOnlyList<string> Parameters { get; init; } = [];

    /// <summary>
    /// Gets function symbols imported or declared only inside this scope.
    /// </summary>
    public IReadOnlyList<Symbol> Symbols { get; init; } = [];
}
