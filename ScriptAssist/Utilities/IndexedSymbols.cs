namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// A bind-time symbol list with last-wins name replacement. The index lives only
/// as long as this builder; freeze to a plain list when binding finishes.
/// </summary>
internal sealed class SymbolList : IReadOnlyList<Symbol>
{
    private readonly List<Symbol> _items = [];
    private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);

    public int Count => _items.Count;

    public Symbol this[int index] => _items[index];

    public IEnumerator<Symbol> GetEnumerator() => _items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Replaces any existing symbol of the same name, or appends <paramref name="symbol"/>.
    /// </summary>
    public void Replace(Symbol symbol)
    {
        if (_index.TryGetValue(symbol.Name, out var i))
        {
            _items[i] = symbol;
            return;
        }

        _index[symbol.Name] = _items.Count;
        _items.Add(symbol);
    }

    /// <summary>
    /// Removes the symbol named <paramref name="name"/>, if present.
    /// </summary>
    public void Remove(string name)
    {
        if (!_index.TryGetValue(name, out var i))
        {
            return;
        }

        var last = _items.Count - 1;
        if (i != last)
        {
            var moved = _items[last];
            _items[i] = moved;
            _index[moved.Name] = i;
        }

        _items.RemoveAt(last);
        _index.Remove(name);
    }

    /// <summary>
    /// Gets the symbol named <paramref name="name"/>, if present.
    /// </summary>
    public bool TryGet(string name, out Symbol symbol)
    {
        if (_index.TryGetValue(name, out var i))
        {
            symbol = _items[i];
            return true;
        }

        symbol = null!;
        return false;
    }

    /// <summary>
    /// Returns the inner list and drops this builder from further use.
    /// </summary>
    public List<Symbol> Freeze() => _items;

    /// <summary>
    /// Gets whether <paramref name="members"/> is this builder or its inner list.
    /// </summary>
    public bool Owns(IReadOnlyList<Symbol> members) =>
        ReferenceEquals(this, members) || ReferenceEquals(_items, members);
}
