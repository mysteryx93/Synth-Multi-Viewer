namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Remembers include resolutions and parsed exports until the next refresh.
/// Lookups, publication, clearing, and version checks are synchronized; callers
/// keep file reading and parsing outside the lock.
/// </summary>
internal sealed class IncludeCache
{
    private readonly Lock _gate = new();
    private int _version;
    private readonly Dictionary<(string Specifier, string? From), string?> _paths = [];
    private readonly Dictionary<string, IncludeEntry> _entries = new(StringComparer.Ordinal);

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
            return _paths.TryGetValue((specifier, fromPath), out path);
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

            _paths[(specifier, fromPath)] = path;
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
            return _entries.TryGetValue(path, out entry!);
        }
    }

    /// <summary>
    /// Stores parsed exports for a resolved include path.
    /// </summary>
    public void SetMembers(string path, IReadOnlyList<Symbol> members, int version) =>
        SetEntry(path, new IncludeEntry(Copy(members), []), version);

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

            _entries[path] = entry with
            {
                Members = Copy(entry.Members),
                Dependencies = Copy(entry.Dependencies)
            };
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
            _entries.Clear();
        }
    }

    private static IReadOnlyList<Symbol> Copy(IReadOnlyList<Symbol> members) =>
        members as Symbol[] ?? [..members];

    private static IReadOnlyList<string> Copy(IReadOnlyList<string> items) =>
        items as string[] ?? [..items];
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
        Complete = new HashSet<string>(StringComparer.Ordinal);
    }

    public IncludeCache? Cache { get; }
    public int Version { get; }

    /// <summary>
    /// Paths whose cached graphs were already verified complete during this bind.
    /// </summary>
    public HashSet<string> Complete { get; }

    public bool TryPath(string specifier, string? fromPath, out string? path)
    {
        path = null;
        return Cache != null && Cache.TryPath(specifier, fromPath, out path);
    }

    public void SetPath(string specifier, string? fromPath, string? path) =>
        Cache?.SetPath(specifier, fromPath, path, Version);

    public bool TryMembers(string path, out IReadOnlyList<Symbol> members)
    {
        members = null!;
        return Cache != null && Cache.TryMembers(path, out members);
    }

    public bool TryEntry(string path, out IncludeEntry entry)
    {
        entry = null!;
        return Cache != null && Cache.TryEntry(path, out entry);
    }

    public void SetMembers(string path, IReadOnlyList<Symbol> members) =>
        Cache?.SetMembers(path, members, Version);

    public void SetEntry(string path, IncludeEntry entry) =>
        Cache?.SetEntry(path, entry, Version);
}
