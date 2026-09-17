namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Collects module aliases, core aliases, assignments, and annotations.
/// </summary>
internal static class VapourSynthBinder
{
    /// <summary>
    /// Last assignment wins; seeded <c>vs</c>/<c>core</c> names are overwritten when rebound.
    /// </summary>
    public static DocumentBindings Bind(string text, IReadOnlyList<Symbol> catalog, LexerOptions lexer,
        CancellationToken token, string? documentPath = null, IncludeReader? read = null)
    {
        var clean = BufferLexer.Mask(text, lexer, token: token).Code;
        var quoted = BufferLexer.Mask(text, lexer, maskStrings: false, token: token).Code;
        var names = new Dictionary<string, TypeRef>(StringComparer.Ordinal)
        {
            ["vs"] = VapourSynthTypes.Module,
            ["vapoursynth"] = VapourSynthTypes.Module,
            ["core"] = VapourSynthTypes.Core
        };
        var scriptModules = new Dictionary<string, IReadOnlyList<Symbol>>(StringComparer.Ordinal);
        var modulesByPath = new Dictionary<string, List<Symbol>>(StringComparer.Ordinal);
        var buffer = new List<Symbol>();
        var index = VapourSynthCatalogIndex.Build(catalog);
        var statements = StatementScanner.Scan(clean, token);
        var scopes = FunctionScopes(clean, quoted, statements);

        var n = 0;
        foreach (var span in statements)
        {
            if ((n++ & 63) == 0)
            {
                token.ThrowIfCancellationRequested();
            }

            ApplyStatement(quoted, span, documentPath, read, scriptModules, modulesByPath, lexer, token, buffer,
                names, scopes, index);
        }

        names.Remove("");
        return Current(names, scriptModules, buffer, scopes);
    }

    private static void ApplyStatement(string quoted, StatementScanner.Span span, string? documentPath,
        IncludeReader? read, Dictionary<string, IReadOnlyList<Symbol>> scriptModules,
        Dictionary<string, List<Symbol>> modulesByPath, LexerOptions lexer, CancellationToken token,
        List<Symbol> buffer, Dictionary<string, TypeRef> names, IReadOnlyList<BindingScope> scopes,
        VapourSynthCatalogIndex index)
    {
        var start = span.Start;
        var end = span.End;
        if (Keyword(quoted, start, end, "def"))
        {
            BindDef(quoted, span, scopes, buffer, names, scriptModules, index);
            return;
        }

        var scope = Innermost(scopes, start);
        if (scope != null && start < scope.HeaderEnd)
        {
            return;
        }

        if (Keyword(quoted, start, end, "import"))
        {
            ApplyImport(quoted, AfterKeyword(quoted, start, end, "import"), end, scope, documentPath, read,
                scriptModules, modulesByPath, lexer, token, names, buffer, null);
            return;
        }

        if (Keyword(quoted, start, end, "from"))
        {
            ApplyFrom(quoted, AfterKeyword(quoted, start, end, "from"), end, scope, documentPath, read, scriptModules,
                modulesByPath, lexer, token, buffer, names);
            return;
        }

        if (IsSkippedKeyword(quoted, start, end))
        {
            return;
        }

        TryAssign(quoted, start, end, scope, names, scriptModules, buffer, scopes, index);
    }

    private static void ApplyImport(string quoted, int start, int end, BindingScope? scope, string? documentPath,
        IncludeReader? read, Dictionary<string, IReadOnlyList<Symbol>> scriptModules,
        Dictionary<string, List<Symbol>> modulesByPath, LexerOptions lexer, CancellationToken token,
        Dictionary<string, TypeRef> names, List<Symbol> buffer, List<Symbol>? exports)
    {
        foreach (var part in ParameterNames.Split(quoted[start..end]))
        {
            var spec = part.Trim();
            if (spec.Length == 0)
            {
                continue;
            }

            var alias = spec;
            var imported = spec;
            var asIndex = spec.LastIndexOf(" as ", StringComparison.Ordinal);
            if (asIndex > 0)
            {
                imported = spec[..asIndex].Trim();
                alias = spec[(asIndex + 4)..].Trim();
            }
            else if (imported.Contains('.', StringComparison.Ordinal))
            {
                alias = imported[..imported.IndexOf('.')];
            }

            if (imported.Length == 0 || alias.Length == 0)
            {
                continue;
            }

            BindImportedModule(imported, alias, asIndex > 0, scope, documentPath, read, scriptModules, modulesByPath,
                lexer, token, names, buffer, exports);
        }
    }

    private static void ApplyFrom(string quoted, int start, int end, BindingScope? scope, string? documentPath,
        IncludeReader? read, Dictionary<string, IReadOnlyList<Symbol>> scriptModules,
        Dictionary<string, List<Symbol>> modulesByPath, LexerOptions lexer, CancellationToken token,
        List<Symbol> buffer, Dictionary<string, TypeRef> names)
    {
        var i = start;
        SkipWs(quoted, ref i, end);
        var specStart = i;
        while (i < end && (quoted[i] == '.' || BufferLexer.IsIdentifier(quoted[i])))
        {
            i++;
        }

        var imported = quoted[specStart..i].Trim();
        SkipWs(quoted, ref i, end);
        if (!Keyword(quoted, i, end, "import"))
        {
            return;
        }

        var list = FlattenImportList(quoted[AfterKeyword(quoted, i, end, "import")..end]);
        var target = scope == null ? buffer : ScopeSymbols(scope);
        var targetNames = scope == null ? names : ScopeNames(scope);
        if (imported is "vapoursynth" or "vs")
        {
            foreach (var (source, alias) in ImportNames(list))
            {
                if (source == "core")
                {
                    SetName(alias, VapourSynthTypes.Core, scope, names, buffer);
                }
            }

            return;
        }

        ImportFrom(imported, list, documentPath, read, scriptModules, modulesByPath, lexer, token, target, targetNames);
    }

    private static void BindImportedModule(string imported, string alias, bool explicitAlias, BindingScope? scope,
        string? documentPath, IncludeReader? read, Dictionary<string, IReadOnlyList<Symbol>> scriptModules,
        Dictionary<string, List<Symbol>> modulesByPath, LexerOptions lexer, CancellationToken token,
        Dictionary<string, TypeRef> names, List<Symbol> buffer, List<Symbol>? exports)
    {
        if (imported is "vapoursynth" or "vs")
        {
            SetName(alias, VapourSynthTypes.Module, scope, names, buffer);
            return;
        }

        var loaded = LoadModule(imported, documentPath, read, scriptModules, modulesByPath, lexer, token);
        if (loaded == null)
        {
            return;
        }

        if (!explicitAlias && imported.Contains('.', StringComparison.Ordinal))
        {
            BindDotted(imported, loaded.Value, scope, documentPath, read, scriptModules, modulesByPath, lexer, token,
                names, buffer, exports);
            return;
        }

        var id = AdoptModuleId(alias, loaded.Value.Id, scope, names, scriptModules, buffer);
        ExportAlias(alias, VapourSynthTypes.Script(id), scope, exports);
    }

    private static void BindDotted(string imported, LoadedScript loaded, BindingScope? scope, string? documentPath,
        IncludeReader? read, Dictionary<string, IReadOnlyList<Symbol>> scriptModules,
        Dictionary<string, List<Symbol>> modulesByPath, LexerOptions lexer, CancellationToken token,
        Dictionary<string, TypeRef> names, List<Symbol> buffer, List<Symbol>? exports)
    {
        var parts = imported.Split('.');
        if (parts.Length < 2)
        {
            return;
        }

        string? parentId = null;
        for (var i = 0; i < parts.Length; i++)
        {
            var prefix = string.Join('.', parts, 0, i + 1);
            string id;
            if (i == parts.Length - 1)
            {
                id = loaded.Id;
            }
            else
            {
                var prefixLoaded = LoadModule(prefix, documentPath, read, scriptModules, modulesByPath, lexer, token);
                id = prefixLoaded?.Id ?? NamespaceId(documentPath, prefix);
            }

            if (i == 0)
            {
                parentId = AdoptModuleId(parts[0], id, scope, names, scriptModules, buffer);
                continue;
            }

            var members = MutableModule(scriptModules, parentId!);
            UpsertChild(members, scriptModules, parts[i], id);
            parentId = ChildId(members, parts[i], id);
        }

        ExportAlias(parts[0], VapourSynthTypes.Script(RootId(names, scope, parts[0],
            parentId ?? NamespaceId(documentPath, parts[0]))), scope, exports);
    }

    private static string RootId(Dictionary<string, TypeRef> names, BindingScope? scope, string alias, string fallback)
    {
        var table = scope == null ? names : ScopeNames(scope);
        return table.TryGetValue(alias, out var type)
            ? VapourSynthTypes.ScriptOf(type) ?? fallback
            : fallback;
    }

    private static string ChildId(List<Symbol> members, string child, string fallback)
    {
        foreach (var symbol in members)
        {
            if (symbol.Name.Equals(child, StringComparison.Ordinal))
            {
                return VapourSynthTypes.ScriptOf(new TypeRef(symbol.ReturnType ?? "")) ?? fallback;
            }
        }

        return fallback;
    }

    private static string AdoptModuleId(string alias, string id, BindingScope? scope, Dictionary<string, TypeRef> names,
        Dictionary<string, IReadOnlyList<Symbol>> scriptModules, List<Symbol> buffer)
    {
        var table = scope == null ? names : ScopeNames(scope);
        if (table.TryGetValue(alias, out var existing))
        {
            var oldId = VapourSynthTypes.ScriptOf(existing);
            if (oldId != null && oldId != id)
            {
                var keep = PreferId(oldId, id);
                MergeModules(scriptModules, keep, keep == oldId ? id : oldId);
                id = keep;
            }
            else if (oldId != null)
            {
                id = oldId;
            }
        }

        SetName(alias, VapourSynthTypes.Script(id), scope, names, buffer);
        return id;
    }

    private static string PreferId(string left, string right)
    {
        var leftSynthetic = left.Contains("::", StringComparison.Ordinal);
        var rightSynthetic = right.Contains("::", StringComparison.Ordinal);
        if (leftSynthetic && !rightSynthetic)
        {
            return right;
        }

        if (rightSynthetic && !leftSynthetic)
        {
            return left;
        }

        return left;
    }

    private static void MergeModules(Dictionary<string, IReadOnlyList<Symbol>> scriptModules, string into, string from)
    {
        var dest = MutableModule(scriptModules, into);
        if (!scriptModules.TryGetValue(from, out var src) || ReferenceEquals(src, dest))
        {
            return;
        }

        foreach (var symbol in src)
        {
            UpsertMember(dest, scriptModules, symbol);
        }

        scriptModules[from] = dest;
    }

    private static void UpsertChild(List<Symbol> members, Dictionary<string, IReadOnlyList<Symbol>> scriptModules,
        string child, string childId)
    {
        UpsertMember(members, scriptModules,
            new Symbol(child, null, SymbolKind.Namespace, ReturnType: VapourSynthTypes.Script(childId).Id));
    }

    private static void UpsertMember(List<Symbol> members, Dictionary<string, IReadOnlyList<Symbol>> scriptModules,
        Symbol incoming)
    {
        for (var i = 0; i < members.Count; i++)
        {
            if (!members[i].Name.Equals(incoming.Name, StringComparison.Ordinal))
            {
                continue;
            }

            var existingId = members[i].ReturnType is { } existingReturn
                ? VapourSynthTypes.ScriptOf(new TypeRef(existingReturn))
                : null;
            var incomingId = incoming.ReturnType is { } incomingReturn
                ? VapourSynthTypes.ScriptOf(new TypeRef(incomingReturn))
                : null;
            if (existingId != null && incomingId != null && existingId != incomingId)
            {
                var keep = PreferId(existingId, incomingId);
                MergeModules(scriptModules, keep, keep == existingId ? incomingId : existingId);
                members[i] = incoming with { ReturnType = VapourSynthTypes.Script(keep).Id };
                return;
            }

            if (incoming.Kind == SymbolKind.Namespace || members[i].Parameters == null)
            {
                members[i] = incoming with { ReturnType = incoming.ReturnType ?? members[i].ReturnType };
            }

            return;
        }

        members.Add(incoming);
    }

    private static string NamespaceId(string? documentPath, string prefix) =>
        (documentPath ?? "") + "::" + prefix;

    private static List<Symbol> MutableModule(Dictionary<string, IReadOnlyList<Symbol>> scriptModules, string id)
    {
        if (scriptModules.TryGetValue(id, out var existing) && existing is List<Symbol> list)
        {
            return list;
        }

        var created = existing == null ? [] : existing.ToList();
        scriptModules[id] = created;
        return created;
    }

    private static void ExportAlias(string alias, TypeRef type, BindingScope? scope, List<Symbol>? exports)
    {
        var symbol = new Symbol(alias, null, SymbolKind.Namespace, ReturnType: type.Id);
        if (scope != null)
        {
            ReplaceSymbol(ScopeSymbols(scope), symbol);
        }

        if (exports != null)
        {
            ReplaceSymbol(exports, symbol);
        }
    }

    private static void TryAssign(string quoted, int start, int end, BindingScope? scope,
        Dictionary<string, TypeRef> names, Dictionary<string, IReadOnlyList<Symbol>> scriptModules, List<Symbol> buffer,
        IReadOnlyList<BindingScope> scopes, VapourSynthCatalogIndex index)
    {
        var i = start;
        if (!TryIdent(quoted, ref i, end, out var name))
        {
            return;
        }

        SkipWs(quoted, ref i, end);
        if (i < end && quoted[i] == ',')
        {
            var unpack = new List<string> { name };
            while (i < end && quoted[i] == ',')
            {
                i++;
                SkipWs(quoted, ref i, end);
                if (!TryIdent(quoted, ref i, end, out var next))
                {
                    return;
                }

                unpack.Add(next);
                SkipWs(quoted, ref i, end);
            }

            SkipWs(quoted, ref i, end);
            if (i >= end || ParameterNames.KeywordEqualsIndex(quoted[i..end]) != 0)
            {
                return;
            }

            foreach (var item in unpack)
            {
                SetName(item, TypeRef.Unknown, scope, names, buffer);
            }

            return;
        }

        var annotation = "";
        if (i < end && quoted[i] == ':')
        {
            i++;
            var annStart = i;
            var depth = 0;
            while (i < end)
            {
                var c = quoted[i];
                if (c is '(' or '[' or '{')
                {
                    depth++;
                }
                else if (c is ')' or ']' or '}' && depth > 0)
                {
                    depth--;
                }
                else if (depth == 0 && ParameterNames.KeywordEqualsIndex(quoted[i..end]) == 0)
                {
                    break;
                }

                i++;
            }

            annotation = quoted[annStart..i].Trim();
            SkipWs(quoted, ref i, end);
        }

        var type = VapourSynthTypes.FromAnnotation(annotation);
        if (i < end && ParameterNames.KeywordEqualsIndex(quoted[i..end]) == 0)
        {
            i++;
            var rhs = quoted[i..end].Trim();
            var inferred = VapourSynthTypeWalker.Infer(rhs, ForInfer(names, scriptModules, buffer, scopes, start),
                index);
            if (inferred.IsRoot)
            {
                inferred = TypeRef.Unknown;
            }

            if (!inferred.IsUnknown)
            {
                type = inferred;
            }
        }
        else if (type.IsUnknown)
        {
            return;
        }

        SetName(name, type, scope, names, buffer);
    }

    private static void SetName(string name, TypeRef type, BindingScope? scope, Dictionary<string, TypeRef> names,
        List<Symbol>? buffer = null)
    {
        if (scope != null)
        {
            ScopeNames(scope)[name] = type;
            RemoveSymbol(ScopeSymbols(scope), name);
            return;
        }

        names[name] = type;
        if (buffer != null)
        {
            RemoveSymbol(buffer, name);
        }
    }

    private static void BindFunction(Symbol symbol, BindingScope? scope, List<Symbol> buffer,
        Dictionary<string, TypeRef> names)
    {
        if (scope != null)
        {
            ScopeNames(scope).Remove(symbol.Name);
            ReplaceSymbol(ScopeSymbols(scope), symbol);
            return;
        }

        names.Remove(symbol.Name);
        ReplaceSymbol(buffer, symbol);
    }

    private static void ReplaceSymbol(List<Symbol> target, Symbol symbol)
    {
        RemoveSymbol(target, symbol.Name);
        target.Add(symbol);
    }

    private static void RemoveSymbol(List<Symbol> target, string name) =>
        target.RemoveAll(symbol => symbol.Name.Equals(name, StringComparison.Ordinal));

    private static void BindDef(string quoted, StatementScanner.Span span, IReadOnlyList<BindingScope> scopes,
        List<Symbol> buffer, Dictionary<string, TypeRef> names, Dictionary<string, IReadOnlyList<Symbol>> scriptModules,
        VapourSynthCatalogIndex index)
    {
        var self = Innermost(scopes, span.Start);
        if (self == null || self.Start != span.Start || !TryDef(quoted, span, out _, out var open))
        {
            return;
        }

        BindParameters(self.Parameters, ScopeNames(self), ForInfer(names, scriptModules, buffer, scopes, span.Start),
            index);
        var parameters = self.Parameters as string[] ?? [..self.Parameters];
        var close = FunctionHeaders.MatchingClose(quoted, open);
        var returnType = close >= 0 ? VapourSynthFunctions.ReturnId(quoted, close) : null;
        BindFunction(new Symbol(self.Name, parameters, ReturnType: returnType), Parent(scopes, self), buffer, names);
        if (self.HeaderEnd >= span.End)
        {
            return;
        }

        var i = self.HeaderEnd;
        SkipWs(quoted, ref i, span.End);
        if (i < span.End)
        {
            TryAssign(quoted, i, span.End, self, names, scriptModules, buffer, scopes, index);
        }
    }

    private static BindingScope? Parent(IReadOnlyList<BindingScope> scopes, BindingScope child)
    {
        BindingScope? parent = null;
        foreach (var scope in scopes)
        {
            if (ReferenceEquals(scope, child) || child.Start < scope.Start || child.Start > scope.End)
            {
                continue;
            }

            if (parent == null || scope.Start >= parent.Start)
            {
                parent = scope;
            }
        }

        return parent;
    }

    private static List<BindingScope> FunctionScopes(string clean, string quoted,
        IReadOnlyList<StatementScanner.Span> statements)
    {
        var scopes = new List<BindingScope>();
        for (var i = 0; i < statements.Count; i++)
        {
            var span = statements[i];
            if (!TryDef(quoted, span, out var name, out var open))
            {
                continue;
            }

            var close = FunctionHeaders.MatchingClose(clean, open);
            if (close < 0)
            {
                continue;
            }

            var parameters = ParameterNames.Split(quoted[(open + 1)..close]);
            var headerEnd = HeaderColon(clean, close);
            scopes.Add(new BindingScope
            {
                Start = span.Start,
                End = BlockEnd(clean, statements, i, span, headerEnd),
                Name = name,
                HeaderEnd = headerEnd,
                Names = new Dictionary<string, TypeRef>(StringComparer.Ordinal),
                Parameters = parameters,
                Symbols = new List<Symbol>()
            });
        }

        return scopes;
    }

    private static bool TryDef(string quoted, StatementScanner.Span span, out string name, out int open)
    {
        name = "";
        open = -1;
        if (!Keyword(quoted, span.Start, span.End, "def"))
        {
            return false;
        }

        var i = AfterKeyword(quoted, span.Start, span.End, "def");
        if (!TryIdent(quoted, ref i, span.End, out name))
        {
            return false;
        }

        SkipWs(quoted, ref i, span.End);
        if (i >= span.End || quoted[i] != '(')
        {
            return false;
        }

        open = i;
        return true;
    }

    private static void BindParameters(IReadOnlyList<string> parameters, Dictionary<string, TypeRef> names,
        DocumentBindings bindings, VapourSynthCatalogIndex index)
    {
        foreach (var parameter in parameters)
        {
            var name = ParameterNames.OfPython(parameter);
            if (name == null)
            {
                continue;
            }

            names[name] = ParameterType(parameter, name, bindings, index);
        }
    }

    private static TypeRef ParameterType(string parameter, string name, DocumentBindings bindings,
        VapourSynthCatalogIndex index)
    {
        var text = parameter.Trim();
        var eq = ParameterNames.KeywordEqualsIndex(text);
        var colon = text.IndexOf(':');
        var type = TypeRef.Unknown;
        if (colon > 0 && (eq < 0 || colon < eq))
        {
            var annotation = (eq < 0 ? text[(colon + 1)..] : text[(colon + 1)..eq]).Trim();
            type = VapourSynthTypes.FromAnnotation(annotation);
        }

        if (type.IsUnknown && eq >= 0)
        {
            type = VapourSynthTypeWalker.Infer(text[(eq + 1)..].Trim(), bindings, index);
        }

        if (type.IsUnknown && name == "clip")
        {
            type = VapourSynthTypes.VideoNode;
        }

        return type;
    }

    private static int HeaderColon(string text, int parenClose)
    {
        var i = parenClose + 1;
        while (i < text.Length && text[i] is ' ' or '\t')
        {
            i++;
        }

        if (i + 1 < text.Length && text[i] == '-' && text[i + 1] == '>')
        {
            i += 2;
            while (i < text.Length && text[i] is not ':' and not '\n' and not '\r')
            {
                i++;
            }
        }

        while (i < text.Length && text[i] is not ':' and not '\n' and not '\r')
        {
            i++;
        }

        return i < text.Length && text[i] == ':' ? i + 1 : parenClose + 1;
    }

    private static int BlockEnd(string text, IReadOnlyList<StatementScanner.Span> statements, int defIndex,
        StatementScanner.Span def, int headerEnd)
    {
        var i = headerEnd;
        while (i < def.End && i < text.Length && text[i] is ' ' or '\t')
        {
            i++;
        }

        if (i < def.End && i < text.Length && text[i] is not '\n' and not '\r' and not '#')
        {
            var line = LineStart(text, def.Start);
            var end = def.End;
            for (var n = defIndex + 1; n < statements.Count; n++)
            {
                if (LineStart(text, statements[n].Start) != line)
                {
                    break;
                }

                end = statements[n].End;
            }

            return end;
        }

        var defIndent = IndentAt(text, def.Start);
        var bodyIndent = -1;
        for (var n = defIndex + 1; n < statements.Count; n++)
        {
            var statement = statements[n];
            var indent = IndentAt(text, statement.Start);
            if (bodyIndent < 0)
            {
                if (indent <= defIndent)
                {
                    var start = LineStart(text, statement.Start);
                    return start == 0 ? headerEnd : start - 1;
                }

                bodyIndent = indent;
                continue;
            }

            if (indent < bodyIndent)
            {
                var start = LineStart(text, statement.Start);
                return start == 0 ? headerEnd : start - 1;
            }
        }

        return text.Length;
    }

    private static int IndentAt(string text, int offset) => LineIndent(text, LineStart(text, offset));

    private static int LineStart(string text, int offset)
    {
        var i = offset;
        while (i > 0 && text[i - 1] is not '\n' and not '\r')
        {
            i--;
        }

        return i;
    }

    private static int LineIndent(string text, int lineStart)
    {
        var n = 0;
        var i = lineStart;
        while (i < text.Length && text[i] is ' ' or '\t')
        {
            n += text[i] == '\t' ? 4 : 1;
            i++;
        }

        return n;
    }

    private static DocumentBindings ForInfer(Dictionary<string, TypeRef> names,
        Dictionary<string, IReadOnlyList<Symbol>> scriptModules, List<Symbol> buffer,
        IReadOnlyList<BindingScope> scopes, int offset) =>
        Current(names, scriptModules, buffer, scopes).At(offset);

    private static BindingScope? Innermost(IReadOnlyList<BindingScope> scopes, int offset)
    {
        BindingScope? inner = null;
        foreach (var scope in scopes)
        {
            if (offset < scope.Start || offset > scope.End)
            {
                continue;
            }

            if (inner == null || scope.Start >= inner.Start)
            {
                inner = scope;
            }
        }

        return inner;
    }

    private static void ImportFrom(string imported, string list, string? documentPath, IncludeReader? read,
        Dictionary<string, IReadOnlyList<Symbol>> scriptModules, Dictionary<string, List<Symbol>> modulesByPath,
        LexerOptions lexer, CancellationToken token, List<Symbol> target, Dictionary<string, TypeRef>? names)
    {
        list = FlattenImportList(list);
        if (imported.Length > 0 && imported.All(c => c == '.'))
        {
            foreach (var (source, alias) in ImportNames(list))
            {
                if (source == "*")
                {
                    continue;
                }

                var loaded = LoadModule(imported + source, documentPath, read, scriptModules, modulesByPath, lexer,
                    token);
                BindImported(loaded, alias, target, names);
            }

            return;
        }

        var script = LoadModule(imported, documentPath, read, scriptModules, modulesByPath, lexer, token);
        foreach (var (source, alias) in ImportNames(list))
        {
            if (source == "*")
            {
                if (script == null)
                {
                    continue;
                }

                foreach (var symbol in script.Value.Members)
                {
                    Export(symbol, target, names);
                }

                continue;
            }

            var match = script?.Members.FirstOrDefault(x => x.Name.Equals(source, StringComparison.Ordinal));
            if (match != null)
            {
                Export(match.Name == alias ? match : match with { Name = alias }, target, names);
            }
        }
    }

    private static void BindImported(LoadedScript? script, string alias, List<Symbol> target,
        Dictionary<string, TypeRef>? names)
    {
        if (script == null)
        {
            return;
        }

        Export(new Symbol(alias, null, SymbolKind.Namespace, ReturnType: VapourSynthTypes.Script(script.Value.Id).Id),
            target, names);
    }

    private static void Export(Symbol symbol, List<Symbol> target, Dictionary<string, TypeRef>? names)
    {
        ReplaceSymbol(target, symbol);
        var script = symbol.ReturnType != null ? VapourSynthTypes.ScriptOf(new TypeRef(symbol.ReturnType)) : null;
        if (script != null && names != null)
        {
            names[symbol.Name] = VapourSynthTypes.Script(script);
            return;
        }

        names?.Remove(symbol.Name);
    }

    private static IEnumerable<(string Source, string Alias)> ImportNames(string list)
    {
        foreach (var part in ParameterNames.Split(FlattenImportList(list)))
        {
            var piece = part.Trim();
            if (piece.Length == 0)
            {
                continue;
            }

            if (piece == "*")
            {
                yield return ("*", "*");
                continue;
            }

            var asIndex = piece.LastIndexOf(" as ", StringComparison.Ordinal);
            var alias = (asIndex < 0 ? piece : piece[(asIndex + 4)..]).Trim();
            var source = asIndex < 0 ? piece : piece[..asIndex].Trim();
            if (alias.Length > 0 && source.Length > 0)
            {
                yield return (source, alias);
            }
        }
    }

    private static LoadedScript? LoadModule(string imported, string? documentPath, IncludeReader? read,
        Dictionary<string, IReadOnlyList<Symbol>> scriptModules, Dictionary<string, List<Symbol>> modulesByPath,
        LexerOptions lexer, CancellationToken token)
    {
        if (imported is "vapoursynth" or "vs" || read == null)
        {
            return null;
        }

        var file = read(imported, documentPath);
        if (file == null)
        {
            return null;
        }

        if (modulesByPath.TryGetValue(file.Value.Path, out var existing))
        {
            scriptModules[file.Value.Path] = existing;
            return new LoadedScript(file.Value.Path, existing);
        }

        var members = new List<Symbol>();
        scriptModules[file.Value.Path] = members;
        modulesByPath[file.Value.Path] = members;
        FillModule(file.Value.Text, file.Value.Path, members, read, scriptModules, modulesByPath, lexer, token);
        return new LoadedScript(file.Value.Path, members);
    }

    private static void FillModule(string text, string path, List<Symbol> members, IncludeReader? read,
        Dictionary<string, IReadOnlyList<Symbol>> scriptModules, Dictionary<string, List<Symbol>> modulesByPath,
        LexerOptions lexer, CancellationToken token)
    {
        var clean = BufferLexer.Mask(text, lexer, token: token).Code;
        var quoted = BufferLexer.Mask(text, lexer, maskStrings: false, token: token).Code;
        var statements = StatementScanner.Scan(clean, token);
        var scopes = FunctionScopes(clean, quoted, statements);
        var extras = new List<Symbol>();
        var dummy = new Dictionary<string, TypeRef>(StringComparer.Ordinal);
        foreach (var span in statements)
        {
            token.ThrowIfCancellationRequested();
            var inner = Innermost(scopes, span.Start);
            if (inner != null && span.Start != inner.Start)
            {
                continue;
            }

            if (Keyword(quoted, span.Start, span.End, "def"))
            {
                if (inner == null || IndentAt(clean, span.Start) != 0)
                {
                    continue;
                }

                if (!TryDef(quoted, span, out _, out var open))
                {
                    continue;
                }

                var parameters = inner.Parameters as string[] ?? [..inner.Parameters];
                var close = FunctionHeaders.MatchingClose(quoted, open);
                var returnType = close >= 0 ? VapourSynthFunctions.ReturnId(quoted, close) : null;
                ReplaceSymbol(extras, new Symbol(inner.Name, parameters, ReturnType: returnType));
                continue;
            }

            if (Keyword(quoted, span.Start, span.End, "import"))
            {
                ApplyImport(quoted, AfterKeyword(quoted, span.Start, span.End, "import"), span.End, null, path, read,
                    scriptModules, modulesByPath, lexer, token, dummy, extras, extras);
                continue;
            }

            if (Keyword(quoted, span.Start, span.End, "from"))
            {
                ApplyFrom(quoted, AfterKeyword(quoted, span.Start, span.End, "from"), span.End, null, path, read,
                    scriptModules, modulesByPath, lexer, token, extras, dummy);
            }
        }

        members.AddRange(extras);
    }

    private static string FlattenImportList(string list)
    {
        var text = list.Trim();
        if (text.StartsWith('('))
        {
            var close = text.LastIndexOf(')');
            if (close > 0)
            {
                text = text[1..close];
            }
        }

        return text;
    }

    private static bool Keyword(string text, int start, int end, string word)
    {
        if (end - start < word.Length)
        {
            return false;
        }

        if (!text.AsSpan(start, word.Length).Equals(word, StringComparison.Ordinal))
        {
            return false;
        }

        var after = start + word.Length;
        return after == end || !BufferLexer.IsIdentifier(text[after]);
    }

    private static bool IsSkippedKeyword(string text, int start, int end) =>
        Keyword(text, start, end, "def") || Keyword(text, start, end, "class") ||
        Keyword(text, start, end, "if") || Keyword(text, start, end, "elif") ||
        Keyword(text, start, end, "else") || Keyword(text, start, end, "for") ||
        Keyword(text, start, end, "while") || Keyword(text, start, end, "try") ||
        Keyword(text, start, end, "except") || Keyword(text, start, end, "finally") ||
        Keyword(text, start, end, "with") || Keyword(text, start, end, "return") ||
        Keyword(text, start, end, "pass") || Keyword(text, start, end, "raise") ||
        Keyword(text, start, end, "assert") || Keyword(text, start, end, "yield") ||
        Keyword(text, start, end, "async") || Keyword(text, start, end, "lambda") ||
        Keyword(text, start, end, "global") || Keyword(text, start, end, "del");

    private static int AfterKeyword(string text, int start, int end, string word)
    {
        var i = start + word.Length;
        SkipWs(text, ref i, end);
        return i;
    }

    private static void SkipWs(string text, ref int i, int end)
    {
        while (i < end && char.IsWhiteSpace(text[i]))
        {
            i++;
        }
    }

    private static bool TryIdent(string text, ref int i, int end, out string name)
    {
        var start = i;
        if (i >= end || !BufferLexer.IsIdentifier(text[i]) || char.IsDigit(text[i]))
        {
            name = "";
            return false;
        }

        i++;
        while (i < end && BufferLexer.IsIdentifier(text[i]))
        {
            i++;
        }

        name = text[start..i];
        return true;
    }

    private static Dictionary<string, TypeRef> ScopeNames(BindingScope scope) =>
        (Dictionary<string, TypeRef>)scope.Names;

    private static List<Symbol> ScopeSymbols(BindingScope scope) => (List<Symbol>)scope.Symbols;

    private static DocumentBindings Current(Dictionary<string, TypeRef> names,
        Dictionary<string, IReadOnlyList<Symbol>> scriptModules, List<Symbol> buffer,
        IReadOnlyList<BindingScope>? scopes = null) =>
        new()
        {
            Names = names,
            ScriptModules = scriptModules,
            BufferSymbols = buffer,
            Scopes = scopes ?? []
        };

    private readonly record struct LoadedScript(string Id, IReadOnlyList<Symbol> Members);
}
