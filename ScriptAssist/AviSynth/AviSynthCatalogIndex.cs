using System.Runtime.CompilerServices;

namespace HanumanInstitute.ScriptAssist.AviSynth;

/// <summary>
/// Groups AviSynth catalog functions by ordinal-ignore-case name, preserving overload order.
/// </summary>
internal sealed class AviSynthCatalogIndex
{
    private static readonly ConditionalWeakTable<IReadOnlyList<Symbol>, AviSynthCatalogIndex> Indexes = new();

    private readonly Dictionary<string, IReadOnlyList<Symbol>> _functions =
        new(StringComparer.OrdinalIgnoreCase);

    private AviSynthCatalogIndex()
    {
    }

    /// <summary>
    /// Builds or reuses an index of <paramref name="catalog"/>.
    /// </summary>
    public static AviSynthCatalogIndex Build(IReadOnlyList<Symbol> catalog) =>
        Indexes.GetValue(catalog, static symbols => Create(symbols));

    private static AviSynthCatalogIndex Create(IReadOnlyList<Symbol> catalog)
    {
        var index = new AviSynthCatalogIndex();
        foreach (var group in catalog.GroupBy(symbol => symbol.Name, StringComparer.OrdinalIgnoreCase))
        {
            var functions = new List<Symbol>();
            foreach (var symbol in group)
            {
                if (symbol.Kind == SymbolKind.Function)
                {
                    functions.Add(symbol);
                }
            }

            if (functions.Count > 0)
            {
                index._functions[group.Key] = functions;
            }
        }

        return index;
    }

    /// <summary>
    /// Gets the first function named <paramref name="name"/>, or null.
    /// </summary>
    public Symbol? Find(string name) =>
        _functions.TryGetValue(name, out var group) ? group[0] : null;
}
