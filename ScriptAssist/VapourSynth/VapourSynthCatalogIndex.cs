using System.Runtime.CompilerServices;

namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Groups catalog functions by plugin namespace for member lookup.
/// </summary>
internal sealed class VapourSynthCatalogIndex
{
    private static readonly ConditionalWeakTable<IReadOnlyList<Symbol>, VapourSynthCatalogIndex> Indexes = new();

    private readonly Dictionary<string, NamespaceEntry> _namespaces = new(StringComparer.Ordinal);
    private readonly List<Symbol> _namespaceList = [];
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
            if (symbol.Kind == SymbolKind.Namespace)
            {
                continue;
            }

            if (!TrySplit(symbol.Name, out var ns, out var function))
            {
                continue;
            }

            if (!index._namespaces.TryGetValue(ns, out var entry))
            {
                entry = new NamespaceEntry(ns, symbol.Title);
                index._namespaces[ns] = entry;
                index._namespaceList.Add(entry.Namespace);
            }

            var display = VapourSynthTypes.ForDisplay(symbol);
            entry.Functions.Add(display);
            entry.FunctionByName.TryAdd(function, symbol);
            if (VapourSynthArguments.TakesVideo(symbol))
            {
                if (entry.BoundVideoNamespace == null)
                {
                    entry.BoundVideoNamespace = new(ns, null, SymbolKind.Namespace,
                        ReturnType: entry.Namespace.ReturnType);
                    index._boundVideoNamespaces.Add(entry.BoundVideoNamespace);
                }

                entry.BoundVideo.Add(display);
                entry.BoundVideoByName.TryAdd(function, symbol);
            }

            if (VapourSynthArguments.TakesAudio(symbol))
            {
                if (entry.BoundAudioNamespace == null)
                {
                    entry.BoundAudioNamespace = new(ns, null, SymbolKind.Namespace,
                        ReturnType: entry.Namespace.ReturnType);
                    index._boundAudioNamespaces.Add(entry.BoundAudioNamespace);
                }

                entry.BoundAudio.Add(display);
                entry.BoundAudioByName.TryAdd(function, symbol);
            }
        }

        return index;
    }

    /// <summary>
    /// Gets plugin namespaces present on the core.
    /// </summary>
    public IReadOnlyList<Symbol> Namespaces => _namespaceList;

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
        if (!_namespaces.TryGetValue(ns, out var entry))
        {
            return [];
        }

        if (!boundOnly)
        {
            return entry.Functions;
        }

        return node == VapourSynthTypes.AudioNode ? entry.BoundAudio : entry.BoundVideo;
    }

    /// <summary>
    /// Gets the first function named <paramref name="name"/> in <paramref name="ns"/>.
    /// </summary>
    public Symbol? FindFunction(string ns, string name, bool boundOnly, TypeRef node = default)
    {
        if (!_namespaces.TryGetValue(ns, out var entry))
        {
            return null;
        }

        var map = boundOnly
            ? node == VapourSynthTypes.AudioNode ? entry.BoundAudioByName : entry.BoundVideoByName
            : entry.FunctionByName;
        return map.TryGetValue(name, out var symbol) ? symbol : null;
    }

    /// <summary>
    /// Gets the namespace symbol named <paramref name="name"/>, if present.
    /// </summary>
    public Symbol? FindNamespace(string name, bool boundOnly, TypeRef node = default)
    {
        if (!_namespaces.TryGetValue(name, out var entry))
        {
            return null;
        }

        if (!boundOnly)
        {
            return entry.Namespace;
        }

        return node == VapourSynthTypes.AudioNode ? entry.BoundAudioNamespace : entry.BoundVideoNamespace;
    }

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

    private sealed class NamespaceEntry(string ns, string? title)
    {
        public Symbol Namespace { get; } = new(ns, null, SymbolKind.Namespace,
            ReturnType: title.HasValue() ? title : VapourSynthTypes.PluginLabel);
        public List<Symbol> Functions { get; } = [];
        public Dictionary<string, Symbol> FunctionByName { get; } = new(StringComparer.Ordinal);
        public List<Symbol> BoundVideo { get; } = [];
        public Dictionary<string, Symbol> BoundVideoByName { get; } = new(StringComparer.Ordinal);
        public List<Symbol> BoundAudio { get; } = [];
        public Dictionary<string, Symbol> BoundAudioByName { get; } = new(StringComparer.Ordinal);
        public Symbol? BoundVideoNamespace { get; set; }
        public Symbol? BoundAudioNamespace { get; set; }
    }
}
