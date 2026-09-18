using System.Runtime.CompilerServices;

namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Groups catalog functions by plugin namespace for member lookup.
/// </summary>
internal sealed class VapourSynthCatalogIndex
{
    private static readonly ConditionalWeakTable<IReadOnlyList<Symbol>, VapourSynthCatalogIndex> Indexes = new();

    private readonly Dictionary<string, List<Symbol>> _functions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Symbol>> _boundVideo = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Symbol>> _boundAudio = new(StringComparer.Ordinal);
    private readonly List<Symbol> _namespaces = [];
    private readonly List<Symbol> _boundVideoNamespaces = [];
    private readonly List<Symbol> _boundAudioNamespaces = [];

    private VapourSynthCatalogIndex()
    {
    }

    /// <summary>
    /// Builds an index of <c>core.namespace.function</c> symbols.
    /// </summary>
    public static VapourSynthCatalogIndex Build(IReadOnlyList<Symbol> catalog) =>
        Indexes.GetValue(catalog, static symbols => Create(symbols));

    private static VapourSynthCatalogIndex Create(IReadOnlyList<Symbol> catalog)
    {
        var index = new VapourSynthCatalogIndex();
        foreach (var symbol in catalog)
        {
            if (!TrySplit(symbol.Name, out var ns))
            {
                continue;
            }

            if (!index._functions.TryGetValue(ns, out var functions))
            {
                functions = [];
                index._functions[ns] = functions;
                index._namespaces.Add(new(ns, null, SymbolKind.Namespace));
            }

            functions.Add(symbol);
            if (VapourSynthArguments.TakesVideo(symbol))
            {
                AddBound(index._boundVideo, index._boundVideoNamespaces, ns, symbol);
            }

            if (VapourSynthArguments.TakesAudio(symbol))
            {
                AddBound(index._boundAudio, index._boundAudioNamespaces, ns, symbol);
            }
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
    public IReadOnlyList<Symbol> BoundNamespaces(TypeRef node) =>
        node == VapourSynthTypes.AudioNode ? _boundAudioNamespaces : _boundVideoNamespaces;

    /// <summary>
    /// Gets functions in a namespace, optionally only those that bind to a node.
    /// </summary>
    public IReadOnlyList<Symbol> Functions(string ns, bool boundOnly, TypeRef node = default)
    {
        if (!boundOnly)
        {
            return _functions.TryGetValue(ns, out var list) ? list : [];
        }

        var map = node == VapourSynthTypes.AudioNode ? _boundAudio : _boundVideo;
        return map.TryGetValue(ns, out var bound) ? bound : [];
    }

    private static void AddBound(Dictionary<string, List<Symbol>> map, List<Symbol> namespaces, string ns,
        Symbol symbol)
    {
        if (!map.TryGetValue(ns, out var bound))
        {
            bound = [];
            map[ns] = bound;
            namespaces.Add(new(ns, null, SymbolKind.Namespace));
        }

        bound.Add(symbol);
    }

    private static bool TrySplit(string name, out string ns)
    {
        ns = "";
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
        var function = rest[(dot + 1)..];
        return function.Length > 0 && !function.Contains('.', StringComparison.Ordinal);
    }
}
