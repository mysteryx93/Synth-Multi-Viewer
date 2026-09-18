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
        if (Scopes.Count == 0 || !Contains(caret))
        {
            return this;
        }

        var comparer = Names is Dictionary<string, TypeRef> dictionary
            ? dictionary.Comparer
            : StringComparer.Ordinal;
        var merged = new Dictionary<string, TypeRef>(Names, comparer);
        var functions = new Dictionary<string, Symbol>(comparer);
        foreach (var symbol in BufferSymbols)
        {
            functions[symbol.Name] = symbol;
        }

        Overlay(caret, merged, functions);
        return new DocumentBindings
        {
            Names = merged,
            BufferSymbols = [..functions.Values],
            ScriptModules = ScriptModules,
            Scopes = Scopes
        };
    }

    /// <summary>
    /// Gets whether any function scope contains <paramref name="caret"/>.
    /// </summary>
    internal bool Contains(int caret)
    {
        foreach (var scope in Scopes)
        {
            if (caret >= scope.Start && caret <= scope.End)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Overlays containing scope names onto <paramref name="names"/> in source order.
    /// </summary>
    internal void Overlay(int caret, Dictionary<string, TypeRef> names, Dictionary<string, Symbol>? functions = null) =>
        Overlay(Scopes, caret, names, functions);

    /// <summary>
    /// Overlays containing scope names onto <paramref name="names"/> in source order.
    /// A function symbol with parameters removes a same-named value binding.
    /// </summary>
    internal static void Overlay(IReadOnlyList<BindingScope> scopes, int caret, Dictionary<string, TypeRef> names,
        Dictionary<string, Symbol>? functions = null)
    {
        foreach (var scope in scopes)
        {
            if (caret < scope.Start || caret > scope.End)
            {
                continue;
            }

            foreach (var pair in scope.Names)
            {
                names[pair.Key] = pair.Value;
                functions?.Remove(pair.Key);
            }

            if (functions == null)
            {
                continue;
            }

            foreach (var symbol in scope.Symbols)
            {
                functions[symbol.Name] = symbol;
                if (symbol.Parameters != null)
                {
                    names.Remove(symbol.Name);
                }
            }
        }
    }

    /// <summary>
    /// Gets whether <paramref name="caret"/> is inside a function parameter list.
    /// </summary>
    public bool InFunctionHeader(int caret)
    {
        foreach (var scope in Scopes)
        {
            if (caret < scope.Start || caret > scope.End)
            {
                continue;
            }

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
