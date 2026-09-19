namespace HanumanInstitute.ScriptAssist.AviSynth;

/// <summary>
/// Catalog-driven AviSynth profile: <c>last</c>, implicit first clip, and internals.
/// </summary>
public sealed class AviSynthLanguage : ILanguage, IPreparedLanguage, IRefreshableLanguage, IContextHover,
    ICallReceiver
{
    private readonly IncludeReader? _read;
    /// <summary>
    /// Gets AviSynth comment and string rules.
    /// </summary>
    private static LexerOptions LexerOptions { get; } = new()
    {
        HashLineComments = true,
        SlashStarBlocks = true,
        SlashBracketBlocks = true,
        StarBracketBlocks = true,
        TripleQuotes = true,
        DoubledQuotes = true,
        BackslashLineContinuations = true
    };

    /// <summary>
    /// Creates a profile that optionally follows <c>Import</c> through <paramref name="read"/>.
    /// </summary>
    public AviSynthLanguage(IncludeReader? read = null) => _read = read;

    /// <inheritdoc />
    public LexerOptions Lexer => LexerOptions;

    /// <inheritdoc />
    public StringComparison Comparison => StringComparison.OrdinalIgnoreCase;

    /// <inheritdoc />
    public IReadOnlyList<Symbol> Keywords { get; } =
    [
        new("function", null, SymbolKind.Keyword),
        new("return", null, SymbolKind.Keyword),
        new("global", null, SymbolKind.Keyword),
        new("true", null, SymbolKind.Keyword),
        new("false", null, SymbolKind.Keyword),
        new("last", null, SymbolKind.Keyword),
        new("try", null, SymbolKind.Keyword),
        new("catch", null, SymbolKind.Keyword),
        new("if", null, SymbolKind.Keyword),
        new("else", null, SymbolKind.Keyword)
    ];

    /// <inheritdoc />
    internal IncludeCache Includes { get; } = new();

    void IRefreshableLanguage.Invalidate() => Includes.Clear();

    /// <inheritdoc />
    public DocumentBindings Bind(string text, IReadOnlyList<Symbol> catalog, CancellationToken token,
        string? documentPath = null)
    {
        var masked = BufferLexer.Mask(text, Lexer, token: token);
        var quoted = BufferLexer.Mask(text, Lexer, maskStrings: false, token: token);
        return AviSynthBinder.Bind(new PreparedDocument(masked, quoted), catalog, Lexer, token,
            documentPath, _read, Includes);
    }

    DocumentBindings IPreparedLanguage.Bind(PreparedDocument prepared, IReadOnlyList<Symbol> catalog,
        CancellationToken token, string? documentPath) =>
        AviSynthBinder.Bind(prepared, catalog, Lexer, token, documentPath, _read, Includes);

    /// <inheritdoc />
    public string? ParameterName(string parameter) => ParameterNames.OfAviSynth(parameter);

    /// <inheritdoc />
    public TypeRef TypeOf(IReadOnlyList<PathSegment> segments, DocumentBindings bindings, IReadOnlyList<Symbol> catalog) =>
        AviSynthTypeWalker.TypeOf(segments, bindings, catalog);

    /// <inheritdoc />
    public IReadOnlyList<Symbol> Members(TypeRef type, IReadOnlyList<Symbol> catalog, DocumentBindings bindings)
    {
        if (type.IsRoot)
        {
            return Root(catalog, bindings);
        }
        if (type != AviSynthTypes.Clip)
        {
            return [];
        }

        var items = new List<Symbol>();
        var shadowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var symbol in bindings.BufferSymbols)
        {
            if (symbol.Kind != SymbolKind.Function)
            {
                continue;
            }

            shadowed.Add(symbol.Name);
            if (AviSynthTypes.TakesClip(symbol))
            {
                items.Add(symbol);
            }
        }

        foreach (var symbol in catalog)
        {
            if (symbol.Kind != SymbolKind.Function || shadowed.Contains(symbol.Name))
            {
                continue;
            }

            if (AviSynthTypes.TakesClip(symbol))
            {
                items.Add(symbol);
            }
        }

        return items;
    }

    /// <inheritdoc />
    public CallResolution? ResolveCall(IReadOnlyList<PathSegment> callee, DocumentBindings bindings, IReadOnlyList<Symbol> catalog)
    {
        if (callee.Count == 0) { return null; }

        var name = callee[^1].Name;
        var implicitClip = callee.Count > 1;
        var matches = new List<Symbol>();
        foreach (var symbol in bindings.BufferSymbols)
        {
            if (symbol.Kind == SymbolKind.Function && symbol.Name.Equals(name, Comparison))
            {
                AddOverload(matches, symbol);
            }
        }

        if (matches.Count == 0)
        {
            foreach (var symbol in catalog)
            {
                if (symbol.Kind == SymbolKind.Function && symbol.Name.Equals(name, Comparison))
                {
                    AddOverload(matches, symbol);
                }
            }
        }

        if (matches.Count == 0)
        {
            return null;
        }

        return new() { Overloads = matches, ImplicitReceiver = implicitClip };
    }

    bool ICallReceiver.OmitsFirstClip(CallResolution resolved, string firstArgument, DocumentBindings bindings,
        IReadOnlyList<Symbol> catalog, CancellationToken token)
    {
        if (resolved.ImplicitReceiver)
        {
            return true;
        }

        var takesClip = false;
        foreach (var overload in resolved.Overloads)
        {
            if (AviSynthTypes.TakesClip(overload))
            {
                takesClip = true;
                break;
            }
        }

        return takesClip && !SuppliesClip(firstArgument, bindings, catalog, token);
    }

    private bool SuppliesClip(string argument, DocumentBindings bindings, IReadOnlyList<Symbol> catalog,
        CancellationToken token)
    {
        if (!argument.HasText())
        {
            return false;
        }

        if (ParameterNames.TopLevelKeywordEquals(argument) > 0)
        {
            return false;
        }

        var first = argument[0];
        if (first is '"' or '\'' || char.IsDigit(first) || first is '-' or '.')
        {
            return false;
        }

        if (argument.Equals("true", Comparison) || argument.Equals("false", Comparison))
        {
            return false;
        }

        var path = ExpressionReader.Parse(argument, this, token);
        if (path.Count == 0)
        {
            return char.IsLetter(first);
        }

        var type = TypeOf(path, bindings, catalog);
        return type == AviSynthTypes.Clip || type.IsUnknown;
    }

    private static void AddOverload(List<Symbol> matches, Symbol symbol)
    {
        foreach (var existing in matches)
        {
            if (SameOverload(existing, symbol))
            {
                return;
            }
        }

        matches.Add(symbol);
    }

    private static bool SameOverload(Symbol left, Symbol right)
    {
        if (left.ImplicitLast != right.ImplicitLast)
        {
            return false;
        }

        var a = left.Parameters;
        var b = right.Parameters;
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a == null || b == null || a.Length != b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            if (!a[i].Equals(b[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public HoverInfo? Hover(string code, CaretPath path, DocumentBindings bindings, IReadOnlyList<Symbol> catalog) =>
        HoverCore(code, path, bindings, catalog, null);

    HoverInfo? IContextHover.Hover(string code, CaretPath path, DocumentBindings bindings,
        IReadOnlyList<Symbol> catalog, HoverContext? context) =>
        HoverCore(code, path, bindings, catalog, context);

    private HoverInfo? HoverCore(string code, CaretPath path, DocumentBindings bindings, IReadOnlyList<Symbol> catalog,
        HoverContext? context)
    {
        if (path.Start >= path.End || path.End > code.Length)
        {
            return null;
        }

        var name = code[path.Start..path.End];
        if (name.Length == 0)
        {
            return null;
        }

        if (NamedArgumentHover.TryGet(code, path, name, this, bindings, catalog, Comparison, out var parameter,
                context))
        {
            if (parameter == null)
            {
                return null;
            }

            var type = ParameterNames.AviSynthType(parameter);
            if (!type.HasValue() || type.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return new(type, path.Start, path.End - path.Start);
        }

        if (bindings.InFunctionHeader(path.Start) && !IsFunctionName(name, path.Start, bindings))
        {
            return null;
        }

        if (path.Segments.Count == 0 && !Invoked(code, path.End) &&
            bindings.Names.TryGetValue(name, out var local) && !local.IsUnknown &&
            !local.Id.Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            return new(local.Id, path.Start, path.End - path.Start);
        }

        foreach (var symbol in bindings.BufferSymbols)
        {
            if (symbol.Name.Equals(name, Comparison))
            {
                return new(symbol.Signature, path.Start, path.End - path.Start);
            }
        }

        foreach (var symbol in catalog)
        {
            if (symbol.Kind == SymbolKind.Function && symbol.Name.Equals(name, Comparison))
            {
                return new(symbol.Signature, path.Start, path.End - path.Start);
            }
        }

        if (bindings.Names.TryGetValue(name, out var typed) && !typed.IsUnknown &&
            !typed.Id.Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            return new(typed.Id, path.Start, path.End - path.Start);
        }
        return null;
    }

    private static bool IsFunctionName(string name, int offset, DocumentBindings bindings)
    {
        foreach (var scope in bindings.Scopes)
        {
            if (offset >= scope.Start && offset < scope.HeaderEnd &&
                scope.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Invoked(string code, int end)
    {
        var i = end;
        while (i < code.Length && code[i] is ' ' or '\t')
        {
            i++;
        }

        return i < code.Length && code[i] == '(';
    }

    private List<Symbol> Root(IReadOnlyList<Symbol> catalog, DocumentBindings bindings)
    {
        var items = new List<Symbol>(Keywords.Count + catalog.Count + bindings.Names.Count +
            bindings.BufferSymbols.Count);
        items.AddRange(Keywords);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var symbol in bindings.BufferSymbols)
        {
            if (bindings.Names.ContainsKey(symbol.Name) || !seen.Add(symbol.Name))
            {
                continue;
            }

            items.Add(symbol);
        }

        foreach (var symbol in catalog)
        {
            if (bindings.Names.ContainsKey(symbol.Name) || !seen.Add(symbol.Name))
            {
                continue;
            }

            items.Add(symbol);
        }

        foreach (var pair in bindings.Names)
        {
            items.Add(new(pair.Key, null, SymbolKind.Local, ReturnType: pair.Value.Id));
        }

        return items;
    }
}
