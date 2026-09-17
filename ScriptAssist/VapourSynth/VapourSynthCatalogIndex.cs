namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Groups catalog functions by plugin namespace for member lookup.
/// </summary>
internal sealed class VapourSynthCatalogIndex
{
    private readonly Dictionary<string, List<Symbol>> _functions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Symbol>> _boundFunctions = new(StringComparer.Ordinal);
    private readonly List<Symbol> _namespaces = [];
    private readonly List<Symbol> _boundNamespaces = [];

    private VapourSynthCatalogIndex()
    {
    }

    /// <summary>
    /// Builds an index of <c>core.namespace.function</c> symbols.
    /// </summary>
    public static VapourSynthCatalogIndex Build(IReadOnlyList<Symbol> catalog)
    {
        var index = new VapourSynthCatalogIndex();
        foreach (var symbol in catalog)
        {
            if (!TrySplit(symbol.Name, out var ns, out _))
            {
                continue;
            }

            if (!index._functions.TryGetValue(ns, out var functions))
            {
                functions = [];
                index._functions[ns] = functions;
                index._namespaces.Add(new Symbol(ns, null, SymbolKind.Namespace));
            }

            functions.Add(symbol);
            if (!VapourSynthArguments.TakesNode(symbol))
            {
                continue;
            }

            if (!index._boundFunctions.TryGetValue(ns, out var bound))
            {
                bound = [];
                index._boundFunctions[ns] = bound;
                index._boundNamespaces.Add(new Symbol(ns, null, SymbolKind.Namespace));
            }

            bound.Add(symbol);
        }

        return index;
    }

    /// <summary>
    /// Gets plugin namespaces present on the core.
    /// </summary>
    public IReadOnlyList<Symbol> Namespaces => _namespaces;

    /// <summary>
    /// Gets plugin namespaces that bind to a video or audio node.
    /// </summary>
    public IReadOnlyList<Symbol> BoundNamespaces => _boundNamespaces;

    /// <summary>
    /// Gets functions in a namespace, optionally only those that bind to a node.
    /// </summary>
    public IReadOnlyList<Symbol> Functions(string ns, bool boundOnly)
    {
        var map = boundOnly ? _boundFunctions : _functions;
        return map.TryGetValue(ns, out var list) ? list : [];
    }

    /// <summary>
    /// Finds <c>core.namespace.function</c>.
    /// </summary>
    public Symbol? Find(string ns, string name)
    {
        if (!_functions.TryGetValue(ns, out var list))
        {
            return null;
        }

        foreach (var symbol in list)
        {
            if (symbol.Name.EndsWith('.' + name, StringComparison.Ordinal))
            {
                return symbol;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets whether <paramref name="ns"/> is a plugin namespace on the core.
    /// </summary>
    public bool HasNamespace(string ns) => _functions.ContainsKey(ns);

    /// <summary>
    /// Gets whether <paramref name="ns"/> binds to a node.
    /// </summary>
    public bool HasBoundNamespace(string ns) => _boundFunctions.ContainsKey(ns);

    private static bool TrySplit(string name, out string ns, out string function)
    {
        ns = "";
        function = "";
        const string prefix = "core.";
        if (!name.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = name[prefix.Length..];
        var dot = rest.IndexOf('.');
        if (dot <= 0)
        {
            return false;
        }

        ns = rest[..dot];
        function = rest[(dot + 1)..];
        return function.Length > 0 && !function.Contains('.', StringComparison.Ordinal);
    }
}
