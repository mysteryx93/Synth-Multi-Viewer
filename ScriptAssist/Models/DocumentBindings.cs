namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Names and extra symbols collected from one buffer snapshot.
/// </summary>
public sealed class DocumentBindings
{
    /// <summary>
    /// Gets assigned names and their inferred types.
    /// </summary>
    public IReadOnlyDictionary<string, TypeRef> Names { get; init; } =
        new Dictionary<string, TypeRef>(StringComparer.Ordinal);

    /// <summary>
    /// Gets functions declared in the buffer.
    /// </summary>
    public IReadOnlyList<Symbol> BufferSymbols { get; init; } = [];

    /// <summary>
    /// Gets names that alias the script core.
    /// </summary>
    public IReadOnlySet<string> CoreAliases { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets names that alias the host module.
    /// </summary>
    public IReadOnlySet<string> ModuleAliases { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets parsed functions of imported script modules, keyed by module id.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<Symbol>> ScriptModules { get; init; } =
        new Dictionary<string, IReadOnlyList<Symbol>>(StringComparer.Ordinal);

    /// <summary>
    /// Gets function scopes; empty when the language has no nested locals.
    /// </summary>
    public IReadOnlyList<BindingScope> Scopes { get; init; } = [];

    /// <summary>
    /// Returns this snapshot with <see cref="Names"/> overlaid by scopes containing <paramref name="caret"/>.
    /// </summary>
    public DocumentBindings At(int caret)
    {
        if (Scopes.Count == 0)
        {
            return this;
        }

        var comparer = Names is Dictionary<string, TypeRef> dictionary
            ? dictionary.Comparer
            : StringComparer.Ordinal;
        var merged = new Dictionary<string, TypeRef>(Names, comparer);
        foreach (var scope in Scopes)
        {
            if (caret < scope.Start || caret > scope.End)
            {
                continue;
            }

            foreach (var pair in scope.Names)
            {
                merged[pair.Key] = pair.Value;
            }
        }

        return new DocumentBindings
        {
            Names = merged,
            BufferSymbols = BufferSymbols,
            CoreAliases = CoreAliases,
            ModuleAliases = ModuleAliases,
            ScriptModules = ScriptModules,
            Scopes = Scopes
        };
    }

    /// <summary>
    /// Gets whether <paramref name="caret"/> is inside a function parameter list.
    /// </summary>
    public bool InFunctionHeader(int caret)
    {
        foreach (var scope in Scopes)
        {
            if (caret >= scope.Start && caret < scope.HeaderEnd)
            {
                return true;
            }

            if (caret == scope.HeaderEnd && scope.HeaderEnd == scope.End)
            {
                return true;
            }
        }

        return false;
    }
}
