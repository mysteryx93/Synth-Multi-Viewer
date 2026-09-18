namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Catalog-driven VapourSynth profile: bound plugins, vs/core/VideoNode members, same-file types.
/// </summary>
public sealed class VapourSynthLanguage : ILanguage
{
    private readonly IncludeReader? _read;
    /// <summary>
    /// Gets Python-like comment and string rules.
    /// </summary>
    private static LexerOptions LexerOptions { get; } = new()
    {
        HashLineComments = true,
        SingleQuotes = true,
        TripleQuotes = true,
        StringEscapes = true,
        PythonLineContinuations = true
    };

    /// <summary>
    /// Creates a profile that optionally follows Python imports through <paramref name="read"/>.
    /// </summary>
    public VapourSynthLanguage(IncludeReader? read = null) => _read = read;

    /// <inheritdoc />
    public LexerOptions Lexer => LexerOptions;

    /// <inheritdoc />
    public StringComparison Comparison => StringComparison.Ordinal;

    /// <inheritdoc />
    public IReadOnlyList<Symbol> Keywords { get; } =
    [
        new("import", null, SymbolKind.Keyword),
        new("from", null, SymbolKind.Keyword),
        new("as", null, SymbolKind.Keyword),
        new("def", null, SymbolKind.Keyword),
        new("return", null, SymbolKind.Keyword),
        new("if", null, SymbolKind.Keyword),
        new("else", null, SymbolKind.Keyword),
        new("elif", null, SymbolKind.Keyword),
        new("for", null, SymbolKind.Keyword),
        new("while", null, SymbolKind.Keyword),
        new("in", null, SymbolKind.Keyword),
        new("True", null, SymbolKind.Keyword),
        new("False", null, SymbolKind.Keyword),
        new("None", null, SymbolKind.Keyword),
        new("and", null, SymbolKind.Keyword),
        new("or", null, SymbolKind.Keyword),
        new("not", null, SymbolKind.Keyword),
        new("vs", null, SymbolKind.Keyword),
        new("core", null, SymbolKind.Keyword)
    ];

    /// <inheritdoc />
    public DocumentBindings Bind(string text, IReadOnlyList<Symbol> catalog, CancellationToken token,
        string? documentPath = null) =>
        VapourSynthBinder.Bind(text, catalog, Lexer, token, documentPath, _read);

    /// <inheritdoc />
    public double CompletionPriority(Symbol symbol, TypeRef receiver) =>
        VapourSynthTypes.IsNode(receiver) && symbol.Kind != SymbolKind.Namespace ? 1 : 0;

    /// <inheritdoc />
    public string? ParameterName(string parameter) => ParameterNames.OfPython(parameter);

    /// <inheritdoc />
    public TypeRef TypeOf(IReadOnlyList<PathSegment> segments, DocumentBindings bindings, IReadOnlyList<Symbol> catalog) =>
        VapourSynthTypeWalker.TypeOf(segments, bindings, VapourSynthCatalogIndex.Build(catalog));

    /// <inheritdoc />
    public IReadOnlyList<Symbol> Members(TypeRef type, IReadOnlyList<Symbol> catalog, DocumentBindings bindings) =>
        VapourSynthMembers.Of(type, Keywords, bindings, VapourSynthCatalogIndex.Build(catalog));

    /// <inheritdoc />
    public CallResolution? ResolveCall(IReadOnlyList<PathSegment> callee, DocumentBindings bindings, IReadOnlyList<Symbol> catalog)
    {
        if (callee.Count == 0) { return null; }

        var index = VapourSynthCatalogIndex.Build(catalog);
        var name = callee[^1].Name;
        IReadOnlyList<PathSegment> prefix = [];
        if (callee.Count > 1)
        {
            var copy = new PathSegment[callee.Count - 1];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = callee[i];
            }

            prefix = copy;
        }

        var receiver = VapourSynthTypeWalker.TypeOf(prefix, bindings, index);
        var script = VapourSynthTypes.ScriptOf(receiver);
        if (script != null)
        {
            if (!bindings.ScriptModules.TryGetValue(script, out var members))
            {
                return null;
            }

            foreach (var symbol in members)
            {
                if (symbol.Name.Equals(name, StringComparison.Ordinal) && symbol.Parameters != null)
                {
                    return new CallResolution { Overloads = [symbol] };
                }
            }

            return null;
        }

        var ns = VapourSynthTypes.NamespaceOf(receiver);
        if (ns != null)
        {
            var symbol = index.Find(ns, name);
            if (symbol == null)
            {
                return null;
            }

            return new CallResolution
            {
                Overloads = [symbol],
                ImplicitReceiver = VapourSynthTypes.IsBound(receiver)
            };
        }

        var host = receiver == VapourSynthTypes.VideoNode
            ? VapourSynthHostTypes.VideoNodeMembers
            : receiver == VapourSynthTypes.AudioNode
                ? VapourSynthHostTypes.AudioNodeMembers
                : receiver == VapourSynthTypes.Core
                    ? VapourSynthHostTypes.CoreMembers
                    : [];
        foreach (var symbol in host)
        {
            if (symbol.Name.Equals(name, StringComparison.Ordinal) && symbol.Parameters != null)
            {
                return new CallResolution { Overloads = [symbol] };
            }
        }

        if (callee.Count == 1 && bindings.Names.TryGetValue(name, out var aliased))
        {
            var symbol = VapourSynthTypes.FunctionSymbol(aliased);
            if (symbol != null)
            {
                return new CallResolution
                {
                    Overloads = [symbol],
                    ImplicitReceiver = VapourSynthTypes.IsBoundFunction(aliased)
                };
            }
        }

        if (callee.Count == 1 && !bindings.Names.ContainsKey(name))
        {
            List<Symbol>? local = null;
            foreach (var symbol in bindings.BufferSymbols)
            {
                if (symbol.Name.Equals(name, StringComparison.Ordinal) && symbol.Parameters != null)
                {
                    local ??= [];
                    local.Add(symbol);
                }
            }

            if (local != null)
            {
                return new CallResolution { Overloads = local };
            }
        }

        return null;
    }

    /// <inheritdoc />
    public HoverInfo? Hover(string code, CaretPath path, DocumentBindings bindings, IReadOnlyList<Symbol> catalog)
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

        if (NamedArgumentHover.TryGet(code, path, name, this, bindings, catalog, Comparison, out var parameter))
        {
            return parameter == null ? null : TypeHover(name, path, ParameterType(parameter));
        }

        if (bindings.InFunctionHeader(path.Start) && path.Segments.Count == 0 &&
            IsParameterName(name, path.Start, bindings))
        {
            return null;
        }

        var index = VapourSynthCatalogIndex.Build(catalog);
        var memberType = VapourSynthTypeWalker.TypeOf(WithName(path.Segments, name), bindings, index);
        if (path.Segments.Count > 0)
        {
            var receiver = VapourSynthTypeWalker.TypeOf(path.Segments, bindings, index);
            foreach (var symbol in VapourSynthMembers.Of(receiver, Keywords, bindings, index))
            {
                var inserted = symbol.Name;
                var last = inserted.LastIndexOf('.');
                if (last >= 0)
                {
                    inserted = inserted[(last + 1)..];
                }

                if (!inserted.Equals(name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (symbol.Kind == SymbolKind.Function && symbol.Parameters != null)
                {
                    return new HoverInfo(symbol.Signature, path.Start, path.End - path.Start);
                }

                return TypeHover(name, path, VapourSynthTypes.Display(memberType) ??
                    VapourSynthTypes.DisplayReturn(symbol.ReturnType));
            }

            return TypeHover(name, path, VapourSynthTypes.Display(memberType));
        }

        if (bindings.InFunctionHeader(path.Start) && IsFunctionName(name, path.Start, bindings))
        {
            foreach (var symbol in bindings.BufferSymbols)
            {
                if (symbol.Name.Equals(name, StringComparison.Ordinal) && symbol.Parameters != null)
                {
                    return new HoverInfo(symbol.Signature, path.Start, path.End - path.Start);
                }
            }
        }

        if (!bindings.Names.ContainsKey(name))
        {
            foreach (var symbol in bindings.BufferSymbols)
            {
                if (symbol.Name.Equals(name, StringComparison.Ordinal) && symbol.Parameters != null)
                {
                    return new HoverInfo(symbol.Signature, path.Start, path.End - path.Start);
                }
            }
        }

        if (bindings.Names.TryGetValue(name, out var typed))
        {
            return TypeHover(name, path, VapourSynthTypes.Display(typed));
        }

        return null;
    }

    private static HoverInfo? TypeHover(string name, CaretPath path, string? type)
    {
        if (!type.HasValue() || type.Equals(name, StringComparison.Ordinal))
        {
            return null;
        }

        return new HoverInfo(type, path.Start, path.End - path.Start);
    }

    private static string? ParameterType(string parameter)
    {
        var key = ParameterNames.PythonType(parameter);
        if (!key.HasValue())
        {
            return null;
        }

        var display = VapourSynthTypes.DisplayReturn(key);
        if (display.HasValue() && display != key)
        {
            return display;
        }

        var annotated = VapourSynthTypes.FromAnnotation(key);
        return annotated.IsUnknown ? display ?? key : VapourSynthTypes.Display(annotated);
    }

    private static bool IsParameterName(string name, int offset, DocumentBindings bindings)
    {
        foreach (var scope in bindings.Scopes)
        {
            if (offset >= scope.Start && offset < scope.HeaderEnd && scope.Names.ContainsKey(name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFunctionName(string name, int offset, DocumentBindings bindings)
    {
        foreach (var scope in bindings.Scopes)
        {
            if (offset >= scope.Start && offset < scope.HeaderEnd &&
                scope.Name.Equals(name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<PathSegment> WithName(IReadOnlyList<PathSegment> prefix, string name)
    {
        var segments = new PathSegment[prefix.Count + 1];
        for (var i = 0; i < prefix.Count; i++)
        {
            segments[i] = prefix[i];
        }

        segments[^1] = new PathSegment { Name = name, Kind = PathSegmentKind.Name };
        return segments;
    }
}
