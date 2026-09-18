namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Overloads for the enclosing call and the physical signature slot under the caret.
/// </summary>
/// <param name="Overloads">Candidate signatures for the call.</param>
/// <param name="ActiveParameter">
/// Zero-based index into an overload's <see cref="Symbol.Parameters"/> array. Separators such as
/// <c>*</c> and <c>/</c> occupy slots; the editor maps the current argument onto the displayed
/// overload and highlights that slot. Extra arguments past the last slot are not a parameter.
/// </param>
/// <param name="ImplicitClip">Whether the first clip/node argument is supplied by the receiver.</param>
public sealed record CallInsight(IReadOnlyList<Symbol> Overloads, int ActiveParameter, bool ImplicitClip)
{
    internal string? Keyword { get; init; }

    internal int Positional { get; init; }

    internal IReadOnlySet<string>? UsedNames { get; init; }

    /// <summary>
    /// Language-mapped physical slot for each overload, aligned with <see cref="Overloads"/>.
    /// </summary>
    internal int[]? OverloadSlots { get; init; }

    /// <summary>
    /// Physical parameter index for <paramref name="overloadIndex"/>, using language-aware mapping.
    /// </summary>
    public int GetActiveParameter(int overloadIndex)
    {
        if (OverloadSlots != null && (uint)overloadIndex < (uint)OverloadSlots.Length)
        {
            return OverloadSlots[overloadIndex];
        }

        return ActiveParameter;
    }
}
