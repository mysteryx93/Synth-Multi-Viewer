using System.Text.RegularExpressions;

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
        var names = new Dictionary<string, TypeRef>(StringComparer.Ordinal)
        {
            ["vs"] = VapourSynthTypes.Module,
            ["vapoursynth"] = VapourSynthTypes.Module,
            ["core"] = VapourSynthTypes.Core
        };
        var modules = new HashSet<string>(StringComparer.Ordinal) { "vs", "vapoursynth" };
        var cores = new HashSet<string>(StringComparer.Ordinal) { "core" };
        var scriptModules = new Dictionary<string, IReadOnlyList<Symbol>>(StringComparer.Ordinal);
        var modulesByPath = new Dictionary<string, List<Symbol>>(StringComparer.Ordinal);
        var buffer = new List<Symbol>();

        foreach (Match match in VapourSynthPatterns.ImportVapourSynth().Matches(clean))
        {
            token.ThrowIfCancellationRequested();
            modules.Add(match.Groups[1].Value);
            names[match.Groups[1].Value] = VapourSynthTypes.Module;
        }

        foreach (Match match in VapourSynthPatterns.FromVapourSynthCore().Matches(clean))
        {
            token.ThrowIfCancellationRequested();
            var alias = match.Groups[1].Success && match.Groups[1].Length > 0 ? match.Groups[1].Value : "core";
            cores.Add(alias);
            names[alias] = VapourSynthTypes.Core;
        }

        foreach (Match match in VapourSynthPatterns.ImportAs().Matches(clean))
        {
            token.ThrowIfCancellationRequested();
            var imported = match.Groups[1].Value;
            var alias = match.Groups[2].Success && match.Groups[2].Length > 0
                ? match.Groups[2].Value
                : imported[(imported.LastIndexOf('.') + 1)..];
            var script = LoadModule(imported, documentPath, read, scriptModules, modulesByPath, lexer, token);
            if (script != null)
            {
                names[alias] = VapourSynthTypes.Script(imported);
            }
        }

        foreach (Match match in VapourSynthPatterns.FromImport().Matches(clean))
        {
            token.ThrowIfCancellationRequested();
            ImportFrom(match.Groups[1].Value, match.Groups[2].Value, documentPath, read, scriptModules, modulesByPath,
                lexer, token, buffer, names);
        }

        var quoted = BufferLexer.Mask(text, lexer, maskStrings: false, token: token).Code;
        var index = VapourSynthCatalogIndex.Build(catalog);
        var bindings = Current(names, modules, cores, scriptModules, buffer);
        var scopes = FunctionScopes(clean, quoted, bindings, index);
        bindings = Current(names, modules, cores, scriptModules, buffer, scopes);
        var n = 0;
        foreach (Match match in VapourSynthPatterns.NameAssign().Matches(clean))
        {
            if ((n++ & 63) == 0)
            {
                token.ThrowIfCancellationRequested();
            }

            var name = match.Groups[1].Value;
            var rhs = Slice(quoted, match.Groups[3]);
            var type = VapourSynthTypeWalker.Infer(rhs.Trim(), Visible(bindings, scopes, match.Index), index);
            if (type.IsRoot)
            {
                type = TypeRef.Unknown;
            }

            if (type.IsUnknown)
            {
                type = VapourSynthTypes.FromAnnotation(Slice(quoted, match.Groups[2]).Trim());
            }

            var scope = Innermost(scopes, match.Index);
            if (scope == null)
            {
                names[name] = type;
                if (type == VapourSynthTypes.Core)
                {
                    cores.Add(name);
                }
                else if (name != "core")
                {
                    cores.Remove(name);
                }

                if (type == VapourSynthTypes.Module)
                {
                    modules.Add(name);
                }
            }
            else
            {
                ((Dictionary<string, TypeRef>)scope.Names)[name] = type;
            }

            bindings = Current(names, modules, cores, scriptModules, buffer, scopes);
        }

        foreach (Match match in VapourSynthPatterns.UnpackAssign().Matches(clean))
        {
            token.ThrowIfCancellationRequested();
            var target = Innermost(scopes, match.Index) is { } scope
                ? (Dictionary<string, TypeRef>)scope.Names
                : names;
            foreach (var name in match.Groups[1].Value.Split(','))
            {
                target.TryAdd(name.Trim(), TypeRef.Unknown);
            }
        }

        names.Remove("");
        return Current(names, modules, cores, scriptModules, buffer, scopes);
    }

    private static List<BindingScope> FunctionScopes(string clean, string quoted, DocumentBindings bindings,
        VapourSynthCatalogIndex index)
    {
        var scopes = new List<BindingScope>();
        foreach (Match match in VapourSynthPatterns.AnyDef().Matches(clean))
        {
            var open = match.Index + match.Length - 1;
            var close = FunctionHeaders.MatchingClose(clean, open);
            if (close < 0)
            {
                continue;
            }

            var parameters = ParameterNames.Split(quoted[(open + 1)..close]);
            var headerEnd = HeaderColon(clean, close);
            var names = new Dictionary<string, TypeRef>(StringComparer.Ordinal);
            BindParameters(parameters, names, bindings, index);
            scopes.Add(new BindingScope
            {
                Start = match.Index,
                End = BlockEnd(clean, headerEnd),
                Name = match.Groups[1].Value,
                HeaderEnd = headerEnd,
                Names = names,
                Parameters = parameters
            });
        }

        return scopes;
    }

    private static void BindParameters(IReadOnlyList<string> parameters, Dictionary<string, TypeRef> names,
        DocumentBindings bindings, VapourSynthCatalogIndex index)
    {
        foreach (var parameter in parameters)
        {
            var name = ParameterNames.Of(parameter);
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
        var eq = text.IndexOf('=');
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

    private static int BlockEnd(string text, int headerEnd)
    {
        var i = headerEnd;
        while (i < text.Length && text[i] is ' ' or '\t')
        {
            i++;
        }

        if (i < text.Length && text[i] is not '\n' and not '\r' and not '#')
        {
            return LineEnd(text, i);
        }

        i = NextLineStart(text, headerEnd);
        var bodyIndent = -1;
        while (i < text.Length)
        {
            if (LineIsBlankOrComment(text, i))
            {
                i = NextLineStart(text, i);
                continue;
            }

            var indent = LineIndent(text, i);
            if (bodyIndent < 0)
            {
                if (indent == 0)
                {
                    return i == 0 ? headerEnd : i - 1;
                }

                bodyIndent = indent;
            }
            else if (indent < bodyIndent)
            {
                return i - 1;
            }

            i = NextLineStart(text, i);
        }

        return text.Length;
    }

    private static int LineEnd(string text, int offset)
    {
        while (offset < text.Length && text[offset] is not '\n' and not '\r')
        {
            offset++;
        }

        return offset;
    }

    private static int NextLineStart(string text, int offset)
    {
        while (offset < text.Length && text[offset] is not '\n' and not '\r')
        {
            offset++;
        }

        if (offset < text.Length && text[offset] == '\r')
        {
            offset++;
        }

        if (offset < text.Length && text[offset] == '\n')
        {
            offset++;
        }

        return offset;
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

    private static bool LineIsBlankOrComment(string text, int lineStart)
    {
        var i = lineStart;
        while (i < text.Length && text[i] is ' ' or '\t')
        {
            i++;
        }

        return i == text.Length || text[i] is '\n' or '\r' or '#';
    }

    private static string Slice(string text, Group group) =>
        group is { Success: true, Length: > 0 } && group.Index + group.Length <= text.Length
            ? text.Substring(group.Index, group.Length)
            : "";

    private static DocumentBindings Visible(DocumentBindings bindings, IReadOnlyList<BindingScope> scopes,
        int offset)
    {
        var names = new Dictionary<string, TypeRef>(bindings.Names.Count, StringComparer.Ordinal);
        foreach (var pair in bindings.Names)
        {
            names[pair.Key] = pair.Value;
        }

        foreach (var scope in scopes)
        {
            if (offset < scope.Start || offset > scope.End)
            {
                continue;
            }

            foreach (var pair in scope.Names)
            {
                names[pair.Key] = pair.Value;
            }
        }

        return new DocumentBindings
        {
            Names = names,
            BufferSymbols = bindings.BufferSymbols,
            CoreAliases = bindings.CoreAliases,
            ModuleAliases = bindings.ModuleAliases,
            ScriptModules = bindings.ScriptModules,
            Scopes = scopes
        };
    }

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
        if (imported.Length > 0 && imported.All(c => c == '.'))
        {
            foreach (var (source, alias) in ImportNames(list))
            {
                if (source == "*")
                {
                    continue;
                }

                BindImported(LoadModule(imported + source, documentPath, read, scriptModules, modulesByPath, lexer, token),
                    imported + source, alias, target, names);
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

                foreach (var symbol in script)
                {
                    Export(symbol, target, names);
                }

                continue;
            }

            var match = script?.FirstOrDefault(x => x.Name.Equals(source, StringComparison.Ordinal));
            if (match != null)
            {
                Export(match.Name == alias ? match : match with { Name = alias }, target, names);
            }
        }
    }

    private static void BindImported(IReadOnlyList<Symbol>? script, string imported, string alias, List<Symbol> target,
        Dictionary<string, TypeRef>? names)
    {
        if (script == null)
        {
            return;
        }

        Export(new Symbol(alias, null, SymbolKind.Namespace, ReturnType: VapourSynthTypes.Script(imported).Id), target,
            names);
    }

    private static void Export(Symbol symbol, List<Symbol> target, Dictionary<string, TypeRef>? names)
    {
        target.Add(symbol);
        var script = symbol.ReturnType != null ? VapourSynthTypes.ScriptOf(new TypeRef(symbol.ReturnType)) : null;
        if (script != null && names != null)
        {
            names[symbol.Name] = VapourSynthTypes.Script(script);
        }
    }

    private static IEnumerable<(string Source, string Alias)> ImportNames(string list)
    {
        foreach (var part in list.Split(','))
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

    private static IReadOnlyList<Symbol>? LoadModule(string imported, string? documentPath, IncludeReader? read,
        Dictionary<string, IReadOnlyList<Symbol>> scriptModules, Dictionary<string, List<Symbol>> modulesByPath,
        LexerOptions lexer, CancellationToken token)
    {
        if (imported is "vapoursynth" or "vs" || read == null)
        {
            return null;
        }

        if (scriptModules.TryGetValue(imported, out var cached))
        {
            return cached;
        }

        var file = read(imported, documentPath);
        if (file == null)
        {
            return null;
        }

        if (modulesByPath.TryGetValue(file.Value.Path, out var existing))
        {
            scriptModules[imported] = existing;
            return existing;
        }

        var members = new List<Symbol>();
        scriptModules[imported] = members;
        modulesByPath[file.Value.Path] = members;
        var extras = new List<Symbol>();
        var nestedCode = BufferLexer.Mask(file.Value.Text, lexer, token: token).Code;
        foreach (Match match in VapourSynthPatterns.ImportAs().Matches(nestedCode))
        {
            token.ThrowIfCancellationRequested();
            var nested = match.Groups[1].Value;
            var alias = match.Groups[2].Success && match.Groups[2].Length > 0
                ? match.Groups[2].Value
                : nested[(nested.LastIndexOf('.') + 1)..];
            BindImported(LoadModule(nested, file.Value.Path, read, scriptModules, modulesByPath, lexer, token), nested,
                alias, extras, null);
        }

        foreach (Match match in VapourSynthPatterns.FromImport().Matches(nestedCode))
        {
            token.ThrowIfCancellationRequested();
            ImportFrom(match.Groups[1].Value, match.Groups[2].Value, file.Value.Path, read, scriptModules, modulesByPath,
                lexer, token, extras, null);
        }

        var byName = new Dictionary<string, Symbol>(StringComparer.Ordinal);
        foreach (var symbol in extras)
        {
            byName[symbol.Name] = symbol;
        }

        foreach (var symbol in VapourSynthFunctions.Parse(file.Value.Text, lexer, token))
        {
            byName[symbol.Name] = symbol;
        }

        members.AddRange(byName.Values);
        return members;
    }

    private static DocumentBindings Current(Dictionary<string, TypeRef> names, HashSet<string> modules,
        HashSet<string> cores, Dictionary<string, IReadOnlyList<Symbol>> scriptModules, List<Symbol> buffer,
        IReadOnlyList<BindingScope>? scopes = null) =>
        new()
        {
            Names = names,
            ModuleAliases = modules,
            CoreAliases = cores,
            ScriptModules = scriptModules,
            BufferSymbols = buffer,
            Scopes = scopes ?? []
        };
}
