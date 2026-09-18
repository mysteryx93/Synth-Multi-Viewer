namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Combines a cached catalog with a language profile and a document snapshot cache.
/// </summary>
public sealed class LanguageService : ILanguageService
{
    private readonly ILanguage _language;
    private readonly ISymbolCatalog _catalog;
    private readonly Lock _cacheGate = new();
    private string? _cachedText;
    private string? _cachedPath;
    private IReadOnlyList<Symbol>? _cachedCatalog;
    private DocumentSnapshot? _cachedSnapshot;
    private int _generation;

    /// <summary>
    /// Optional enablement gate. When it returns false, <see cref="GetAsync"/> skips catalog enumeration.
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
    /// Computes completions, nested call insight, and hover from an immutable snapshot.
    /// </summary>
    internal Reply Analyze(string text, int caret, IReadOnlyList<Symbol> native, CancellationToken token = default,
        string? documentPath = null)
    {
        if (caret < 0 || caret > text.Length) { return new([], null); }

        var snapshot = Snapshot(text, native, token, documentPath);
        var rawPrefix = caret == text.Length ? text : text[..caret];
        var prefix = caret == text.Length ? snapshot.Masked
            : BufferLexer.Mask(rawPrefix, _language.Lexer, token: token);
        if (prefix.InLiteral)
        {
            return new([], null);
        }

        token.ThrowIfCancellationRequested();
        var bindings = snapshot.Bindings.At(caret);
        var path = ExpressionReader.Read(snapshot.Masked.Code, caret, _language, token);
        var receiver = _language.TypeOf(path.Segments, bindings, snapshot.Catalog);
        var classified = BufferLexer.Mask(rawPrefix, _language.Lexer, maskStrings: false, token: token).Code;
        var scan = bindings.InFunctionHeader(caret)
            ? null
            : CallScanner.Find(prefix.Code, _language, bindings, snapshot.Catalog, token, classified);
        var insight = scan?.Insight;
        var items = CallScanner.InnermostUnclosed(prefix.Code, token, _language) == '[' &&
            path.Segments.Count == 0
            ? new List<CompletionItem>()
            : Complete(path, receiver, snapshot, bindings, token);
        AddParameterNames(items, path, scan, snapshot.Masked.Code);
        var hover = _language.Hover(snapshot.Masked.Code, path, bindings, snapshot.Catalog);
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
            _cachedText = null;
            _cachedPath = null;
            _cachedCatalog = null;
            _cachedSnapshot = null;
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
            if (_cachedSnapshot != null && _cachedText == text && _cachedPath == documentPath &&
                ReferenceEquals(_cachedCatalog, native))
            {
                return _cachedSnapshot;
            }

            generation = _generation;
        }

        var masked = BufferLexer.Mask(text, _language.Lexer, token: token);
        var bindings = _language.Bind(text, native, token, documentPath);
        var catalog = native;
        if (bindings.BufferSymbols.Count > 0)
        {
            var comparer = _language.Comparison == StringComparison.OrdinalIgnoreCase
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            var bufferNames = new HashSet<string>(bindings.BufferSymbols.Select(symbol => symbol.Name), comparer);
            var combined = new List<Symbol>(native.Count + bindings.BufferSymbols.Count);
            foreach (var symbol in native)
            {
                if (!bufferNames.Contains(symbol.Name))
                {
                    combined.Add(symbol);
                }
            }

            combined.AddRange(bindings.BufferSymbols);
            catalog = combined;
        }

        var snapshot = new DocumentSnapshot(masked, bindings, catalog);
        lock (_cacheGate)
        {
            if (_generation != generation)
            {
                return snapshot;
            }

            _cachedText = text;
            _cachedPath = documentPath;
            _cachedCatalog = native;
            _cachedSnapshot = snapshot;
        }

        return snapshot;
    }

    private sealed record DocumentSnapshot(
        LexedBuffer Masked,
        DocumentBindings Bindings,
        IReadOnlyList<Symbol> Catalog);
}
