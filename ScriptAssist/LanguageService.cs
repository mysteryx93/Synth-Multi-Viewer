namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Combines a cached catalog with a language profile and a document snapshot cache.
/// </summary>
public sealed class LanguageService : ILanguageService
{
    private readonly ILanguage _language;
    private readonly ISymbolCatalog _catalog;
    private readonly Lock _cacheGate = new();
    private readonly List<CachedSnapshot> _snapshots = [];
    private int _generation;
    private const int SnapshotLimit = 8;
    private const long SnapshotByteLimit = 16 * 1024 * 1024;

    /// <summary>
    /// Optional enablement gate. When it returns false, catalog requests are skipped.
    /// </summary>
    internal Func<bool>? AllowRequests { get; set; }

    /// <summary>
    /// Creates a service for <paramref name="language"/> using <paramref name="catalog"/>.
    /// </summary>
    public LanguageService(ILanguage language, ISymbolCatalog catalog)
    {
        _language = language.CheckNotNull();
        _catalog = catalog.CheckNotNull();
    }

    /// <inheritdoc />
    public async Task<Reply> GetAsync(string text, int caret, CancellationToken cancellationToken,
        string? documentPath = null)
    {
        if (AllowRequests?.Invoke() == false)
        {
            return new([], null);
        }

        var native = await _catalog.GetAsync(cancellationToken).ConfigureAwait(false);
        return await Task.Run(() => Analyze(text, caret, native, cancellationToken, documentPath), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Analyzes a snapshot, optionally skipping completion items.
    /// </summary>
    internal Task<Reply> GetAsync(string text, int caret, CancellationToken cancellationToken,
        string? documentPath, bool completions)
    {
        if (AllowRequests?.Invoke() == false)
        {
            return Task.FromResult(new Reply([], null));
        }

        return GetCoreAsync(text, caret, cancellationToken, documentPath, completions);
    }

    private async Task<Reply> GetCoreAsync(string text, int caret, CancellationToken cancellationToken,
        string? documentPath, bool completions)
    {
        var native = await _catalog.GetAsync(cancellationToken).ConfigureAwait(false);
        return await Task.Run(
                () => Analyze(text, caret, native, cancellationToken, documentPath, completions), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Computes completions, nested call insight, and hover from an immutable snapshot.
    /// </summary>
    internal Reply Analyze(string text, int caret, IReadOnlyList<Symbol> native, CancellationToken token = default,
        string? documentPath = null, bool completions = true)
    {
        if (caret < 0 || caret > text.Length) { return new([], null); }

        var snapshot = Snapshot(text, native, token, documentPath);
        if (snapshot.Masked.IsLiteral(caret))
        {
            return new([], null);
        }

        token.ThrowIfCancellationRequested();
        var bindings = snapshot.Bindings.At(caret);
        var path = ExpressionReader.Read(snapshot.Masked.Code, caret, _language, token, snapshot.Joins);
        var receiver = _language.TypeOf(path.Segments, bindings, snapshot.Catalog);
        var walk = bindings.InFunctionHeader(caret)
            ? default
            : CallScanner.Walk(snapshot.Masked.Code, _language, bindings, snapshot.Catalog, token,
                snapshot.Quoted.Code, caret, snapshot.Joins);
        var scan = walk.Scan;
        var insight = scan?.Insight;
        var unclosed = bindings.InFunctionHeader(caret)
            ? CallScanner.InnermostUnclosed(snapshot.Masked.Code, token, _language, caret, snapshot.Joins)
            : walk.Unclosed;
        var items = !completions || unclosed == '[' && path.Segments.Count == 0
            ? new()
            : Complete(path, receiver, snapshot, bindings, token);
        if (completions)
        {
            AddParameterNames(items, path, scan, snapshot.Masked.Code);
        }

        var hoverContext = new HoverContext(scan, unclosed);
        var hover = _language is IContextHover contextual
            ? contextual.Hover(snapshot.Masked.Code, path, bindings, snapshot.Catalog, hoverContext)
            : _language.Hover(snapshot.Masked.Code, path, bindings, snapshot.Catalog);
        var comparison = _language.Comparison;
        var comparer = comparison == StringComparison.OrdinalIgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        return new(items.DistinctBy(x => x.InsertionText, comparer).OrderByDescending(x => x.Priority)
                .ThenBy(x => x.InsertionText, comparer).ToArray(), insight, hover);
    }

    private List<CompletionItem> Complete(CaretPath path, TypeRef receiver, DocumentSnapshot snapshot,
        DocumentBindings bindings, CancellationToken token)
    {
        var members = _language.Members(receiver, snapshot.Catalog, bindings);
        var comparison = _language.Comparison;
        var items = new List<CompletionItem>();
        foreach (var symbol in members)
        {
            token.ThrowIfCancellationRequested();
            var name = InsertionName(symbol.Name);
            if (name.Length == 0 || !name.StartsWith(path.Typed, comparison))
            {
                continue;
            }

            items.Add(new(name, path.Start, path.End - path.Start, symbol.Kind, symbol.Signature,
                _language.CompletionPriority(symbol, receiver)));
        }
        return items;
    }

    private static string InsertionName(string name)
    {
        var last = name.LastIndexOf('.');
        return last < 0 ? name : name[(last + 1)..];
    }

    private static string ParameterInsertion(string name, string code, int end)
    {
        var i = end;
        while (i < code.Length && char.IsWhiteSpace(code[i]))
        {
            i++;
        }

        return i < code.Length && code[i] == '=' ? name : name + "=";
    }

    /// <inheritdoc />
    public void Invalidate()
    {
        lock (_cacheGate)
        {
            _generation++;
            _snapshots.Clear();
        }

        if (_language is IRefreshableLanguage refreshable)
        {
            refreshable.Invalidate();
        }
    }

    private void AddParameterNames(List<CompletionItem> items, CaretPath path, CallScan? scan, string code)
    {
        if (scan == null || path.Segments.Count > 0 || scan.InNestedDelimiter ||
            !ParameterNames.AtArgumentStart(scan.CurrentArgument))
        {
            return;
        }

        var used = scan.UsedNames;
        var seen = new HashSet<string>(_language.Comparison == StringComparison.OrdinalIgnoreCase
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);
        var consumed = scan.PositionalConsumed;
        foreach (var overload in scan.Overloads)
        {
            if (overload.Parameters == null)
            {
                continue;
            }

            var skip = scan.ImplicitClip ? 1 : 0;
            var hasSlash = false;
            foreach (var parameter in overload.Parameters)
            {
                if (parameter.Trim() == "/")
                {
                    hasSlash = true;
                    break;
                }
            }

            var seenSlash = false;
            var keywordOnly = false;
            var index = 0;
            foreach (var parameter in overload.Parameters)
            {
                var kind = ParameterNames.Classify(parameter, ref keywordOnly);
                if (kind is ParameterKind.Separator or ParameterKind.Kwargs or ParameterKind.Varargs)
                {
                    if (parameter.Trim() == "/")
                    {
                        seenSlash = true;
                    }

                    continue;
                }

                if (kind != ParameterKind.KeywordOnly)
                {
                    var slot = index++;
                    if (slot < skip)
                    {
                        continue;
                    }

                    if (slot - skip < consumed || hasSlash && !seenSlash)
                    {
                        continue;
                    }
                }

                var name = _language.ParameterName(parameter);
                if (name == null || used.Contains(name) || !seen.Add(name) ||
                    !name.StartsWith(path.Typed, _language.Comparison))
                {
                    continue;
                }

                items.Add(new(ParameterInsertion(name, code, path.End), path.Start, path.End - path.Start,
                    SymbolKind.Keyword, parameter, 2));
            }
        }
    }

    private DocumentSnapshot Snapshot(string text, IReadOnlyList<Symbol> native, CancellationToken token,
        string? documentPath)
    {
        int generation;
        lock (_cacheGate)
        {
            for (var i = 0; i < _snapshots.Count; i++)
            {
                var item = _snapshots[i];
                if (item.Text == text && item.Path == documentPath && ReferenceEquals(item.Catalog, native))
                {
                    if (i > 0)
                    {
                        _snapshots.RemoveAt(i);
                        _snapshots.Insert(0, item);
                    }

                    return item.Snapshot;
                }
            }

            generation = _generation;
        }

        var masked = BufferLexer.Mask(text, _language.Lexer, token: token);
        var quoted = BufferLexer.Mask(text, _language.Lexer, maskStrings: false, token: token);
        var joins = StatementScanner.Joins(masked.Code, _language, token);
        var bindings = _language.Bind(text, native, token, documentPath);
        var snapshot = new DocumentSnapshot(masked, quoted, joins, bindings, native);
        lock (_cacheGate)
        {
            if (_generation != generation)
            {
                return snapshot;
            }

            if (documentPath != null)
            {
                _snapshots.RemoveAll(item => item.Path == documentPath &&
                    ReferenceEquals(item.Catalog, native));
            }
            else
            {
                _snapshots.RemoveAll(item => item.Text == text && item.Path == null &&
                    ReferenceEquals(item.Catalog, native));
            }

            _snapshots.Insert(0, new(text, documentPath, native, snapshot));
            var bytes = 0L;
            foreach (var item in _snapshots)
            {
                bytes += Size(item);
            }

            while (_snapshots.Count > SnapshotLimit || bytes > SnapshotByteLimit && _snapshots.Count > 1)
            {
                bytes -= Size(_snapshots[^1]);
                _snapshots.RemoveAt(_snapshots.Count - 1);
            }
        }

        return snapshot;
    }

    private static long Size(CachedSnapshot item) =>
        (long)item.Text.Length * sizeof(char) * 3 + item.Snapshot.Joins.Length;

    private sealed record CachedSnapshot(
        string Text,
        string? Path,
        IReadOnlyList<Symbol> Catalog,
        DocumentSnapshot Snapshot);

    private sealed record DocumentSnapshot(
        LexedBuffer Masked,
        LexedBuffer Quoted,
        bool[] Joins,
        DocumentBindings Bindings,
        IReadOnlyList<Symbol> Catalog);
}
