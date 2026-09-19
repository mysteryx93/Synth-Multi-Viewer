namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Remembers include resolutions and parsed exports until the next refresh,
/// with LRU bounds on parsed files and path lookups.
/// Lookups, publication, clearing, and version checks are synchronized; callers
/// keep file reading and parsing outside the lock.
/// </summary>
internal sealed class IncludeCache
{
    private const int EntryLimit = 64;
    private const int PathLimit = 256;
    private const int WorkingSetLimit = 8;
    private readonly Lock _gate = new();
    private int _version;
    private readonly Dictionary<(string Specifier, string? From), string?> _paths = [];
    private readonly LinkedList<(string Specifier, string? From)> _pathOrder = new();
    private readonly Dictionary<(string Specifier, string? From), LinkedListNode<(string Specifier, string? From)>>
        _pathNodes = [];
    private readonly Dictionary<string, IncludeEntry> _entries = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _entryOrder = new();
    private readonly Dictionary<string, LinkedListNode<string>> _entryNodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WorkingSet> _workingSets = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _workingOrder = new();

    /// <summary>
    /// Gets a stamp that changes whenever the cache is cleared.
    /// </summary>
    public int Version
    {
        get
        {
            lock (_gate)
            {
                return _version;
            }
        }
    }

    /// <summary>
    /// Gets a previously resolved path for <paramref name="specifier"/> loaded from <paramref name="fromPath"/>.
    /// </summary>
    public bool TryPath(string specifier, string? fromPath, out string? path)
    {
        lock (_gate)
        {
            var key = (specifier, fromPath);
            if (!_paths.TryGetValue(key, out path))
            {
                return false;
            }

            Touch(key, _pathNodes, _pathOrder);
            return true;
        }
    }

    /// <summary>
    /// Stores the resolved path, or null when the specifier did not load.
    /// </summary>
    public void SetPath(string specifier, string? fromPath, string? path, int version)
    {
        lock (_gate)
        {
            if (version != _version)
            {
                return;
            }

            var key = (specifier, fromPath);
            _paths[key] = path;
            Touch(key, _pathNodes, _pathOrder);
            EvictPaths();
        }
    }

    /// <summary>
    /// Gets parsed exports for a resolved include path.
    /// </summary>
    public bool TryMembers(string path, out IReadOnlyList<Symbol> members)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(path, out var entry))
            {
                Touch(path, _entryNodes, _entryOrder);
                members = entry.Members;
                return true;
            }
        }

        members = null!;
        return false;
    }

    /// <summary>
    /// Gets cached declarations and dependencies for a resolved include path.
    /// </summary>
    public bool TryEntry(string path, out IncludeEntry entry)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(path, out entry!))
            {
                return false;
            }

            Touch(path, _entryNodes, _entryOrder);
            return true;
        }
    }

    /// <summary>
    /// Stores parsed exports for a resolved include path.
    /// </summary>
    public void SetMembers(string path, IReadOnlyList<Symbol> members, int version) =>
        SetEntry(path, new(Copy(members), []), version);

    /// <summary>
    /// Stores declarations and import dependencies for a resolved include path.
    /// </summary>
    public void SetEntry(string path, IncludeEntry entry, int version)
    {
        lock (_gate)
        {
            if (version != _version)
            {
                return;
            }

            _entries[path] = new IncludeEntry(Copy(entry.Members), Copy(entry.Dependencies));
            Touch(path, _entryNodes, _entryOrder);
            EvictEntries();
        }
    }

    /// <summary>
    /// Pins <paramref name="entries"/> and path keys for <paramref name="documentPath"/> so a later
    /// bind of that document does not cascade-reread after LRU eviction.
    /// Restores any working-set rows the LRU dropped during the bind.
    /// </summary>
    public void Retain(string? documentPath, IReadOnlyDictionary<string, IncludeEntry> entries,
        IReadOnlyDictionary<(string Specifier, string? From), string?> paths, int version)
    {
        lock (_gate)
        {
            if (version != _version)
            {
                return;
            }

            var key = documentPath ?? "";
            _workingSets[key] = new WorkingSet(
                new HashSet<string>(entries.Keys, StringComparer.Ordinal),
                [..paths.Keys]);
            Touch(key, _workingOrder);
            while (_workingSets.Count > WorkingSetLimit && _workingOrder.Last != null)
            {
                var last = _workingOrder.Last.Value;
                _workingOrder.RemoveLast();
                _workingSets.Remove(last);
            }

            foreach (var pair in entries)
            {
                _entries[pair.Key] = new IncludeEntry(Copy(pair.Value.Members), Copy(pair.Value.Dependencies));
                Touch(pair.Key, _entryNodes, _entryOrder);
            }

            foreach (var pair in paths)
            {
                _paths[pair.Key] = pair.Value;
                Touch(pair.Key, _pathNodes, _pathOrder);
            }

            EvictEntries();
            EvictPaths();
        }
    }

    /// <summary>
    /// Drops every remembered include.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            _version++;
            _paths.Clear();
            _pathOrder.Clear();
            _pathNodes.Clear();
            _entries.Clear();
            _entryOrder.Clear();
            _entryNodes.Clear();
            _workingSets.Clear();
            _workingOrder.Clear();
        }
    }

    private static void Touch<T>(T key, Dictionary<T, LinkedListNode<T>> nodes, LinkedList<T> order)
        where T : notnull
    {
        if (nodes.TryGetValue(key, out var node))
        {
            order.Remove(node);
            order.AddFirst(node);
            return;
        }

        nodes[key] = order.AddFirst(key);
    }

    private static void Touch(string key, LinkedList<string> order)
    {
        var node = order.Find(key);
        if (node != null)
        {
            order.Remove(node);
            order.AddFirst(node);
            return;
        }

        order.AddFirst(key);
    }

    private void EvictEntries()
    {
        var node = _entryOrder.Last;
        while (_entries.Count > EntryLimit && node != null)
        {
            var previous = node.Previous;
            if (!EntryPinned(node.Value))
            {
                _entries.Remove(node.Value);
                _entryNodes.Remove(node.Value);
                _entryOrder.Remove(node);
            }

            node = previous;
        }
    }

    private void EvictPaths()
    {
        var node = _pathOrder.Last;
        while (_paths.Count > PathLimit && node != null)
        {
            var previous = node.Previous;
            if (!PathPinned(node.Value))
            {
                _paths.Remove(node.Value);
                _pathNodes.Remove(node.Value);
                _pathOrder.Remove(node);
            }

            node = previous;
        }
    }

    private bool EntryPinned(string path)
    {
        foreach (var set in _workingSets.Values)
        {
            if (set.Entries.Contains(path))
            {
                return true;
            }
        }

        return false;
    }

    private bool PathPinned((string Specifier, string? From) key)
    {
        foreach (var set in _workingSets.Values)
        {
            if (set.Paths.Contains(key))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<Symbol> Copy(IReadOnlyList<Symbol> members) =>
        members as Symbol[] ?? [..members];

    private static IReadOnlyList<string> Copy(IReadOnlyList<string> items) =>
        items as string[] ?? [..items];

    private sealed record WorkingSet(
        HashSet<string> Entries,
        HashSet<(string Specifier, string? From)> Paths);
}

/// <summary>
/// Cached declarations of one include file, plus resolved files it imports.
/// </summary>
internal sealed record IncludeEntry(IReadOnlyList<Symbol> Members, IReadOnlyList<string> Dependencies);

/// <summary>
/// Writes include entries using the cache version captured when a bind started.
/// </summary>
internal readonly struct IncludeSession
{
    /// <summary>
    /// Captures <paramref name="cache"/> at the current version so in-flight stores
    /// after <see cref="IncludeCache.Clear"/> are ignored.
    /// </summary>
    public IncludeSession(IncludeCache? cache)
    {
        Cache = cache;
        Version = cache?.Version ?? 0;
        Complete = new(StringComparer.Ordinal);
        Entries = new(StringComparer.Ordinal);
        Paths = [];
    }

    public IncludeCache? Cache { get; }
    public int Version { get; }

    /// <summary>
    /// Paths whose cached graphs were already verified complete during this bind.
    /// </summary>
    public HashSet<string> Complete { get; }

    /// <summary>
    /// Entries loaded during this bind, independent of shared LRU eviction.
    /// </summary>
    public Dictionary<string, IncludeEntry> Entries { get; }

    /// <summary>
    /// Path resolutions loaded during this bind, independent of shared LRU eviction.
    /// </summary>
    public Dictionary<(string Specifier, string? From), string?> Paths { get; }

    public bool TryPath(string specifier, string? fromPath, out string? path)
    {
        var key = (specifier, fromPath);
        if (Paths.TryGetValue(key, out path))
        {
            return true;
        }

        if (Cache == null || !Cache.TryPath(specifier, fromPath, out path))
        {
            path = null;
            return false;
        }

        Paths[key] = path;
        return true;
    }

    public void SetPath(string specifier, string? fromPath, string? path)
    {
        Paths[(specifier, fromPath)] = path;
        Cache?.SetPath(specifier, fromPath, path, Version);
    }

    public bool TryMembers(string path, out IReadOnlyList<Symbol> members)
    {
        if (TryEntry(path, out var entry))
        {
            members = entry.Members;
            return true;
        }

        members = null!;
        return false;
    }

    public bool TryEntry(string path, out IncludeEntry entry)
    {
        if (Entries.TryGetValue(path, out entry!))
        {
            return true;
        }

        if (Cache == null || !Cache.TryEntry(path, out entry))
        {
            entry = null!;
            return false;
        }

        Entries[path] = entry;
        return true;
    }

    public void SetMembers(string path, IReadOnlyList<Symbol> members) =>
        SetEntry(path, new(members, []));

    public void SetEntry(string path, IncludeEntry entry)
    {
        var copy = new IncludeEntry(Copy(entry.Members), Copy(entry.Dependencies));
        Entries[path] = copy;
        Cache?.SetEntry(path, copy, Version);
    }

    /// <summary>
    /// Pins this bind's import graph on the shared cache as the document working set.
    /// </summary>
    public void Finish(string? documentPath) =>
        Cache?.Retain(documentPath, Entries, Paths, Version);

    private static IReadOnlyList<Symbol> Copy(IReadOnlyList<Symbol> members) =>
        members as Symbol[] ?? [..members];

    private static IReadOnlyList<string> Copy(IReadOnlyList<string> items) =>
        items as string[] ?? [..items];
}
