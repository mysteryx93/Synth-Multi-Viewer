using System.Text.RegularExpressions;

namespace HanumanInstitute.ScriptAssist.AviSynth;

/// <summary>
/// Collects assignments, <c>last</c>, and buffer-declared functions.
/// </summary>
internal static class AviSynthBinder
{
    /// <summary>
    /// Script-level last-assignment-wins. Function parameters and inner assignments stay in their scope.
    /// </summary>
    public static DocumentBindings Bind(string text, IReadOnlyList<Symbol> catalog, LexerOptions lexer,
        CancellationToken token, string? documentPath = null, IncludeReader? read = null)
    {
        var joined = AviSynthPatterns.Clean(text, lexer, token: token);
        var names = new Dictionary<string, TypeRef>(StringComparer.OrdinalIgnoreCase)
        {
            ["last"] = AviSynthTypes.Clip
        };
        var spans = AviSynthFunctions.Spans(text, lexer, token);
        var buffer = new List<Symbol>(spans.Count);
        foreach (var span in spans)
        {
            buffer.Add(span.Symbol);
        }

        AviSynthFunctions.AddImports(text, documentPath, read, buffer, new HashSet<string>(StringComparer.Ordinal),
            lexer, token);
        var scopes = FunctionScopes(joined, spans);
        var n = 0;
        foreach (Match match in AviSynthPatterns.NameAssign().Matches(joined))
        {
            if ((n++ & 63) == 0)
            {
                token.ThrowIfCancellationRequested();
            }

            var name = match.Groups[2].Value;
            var type = Infer(match.Groups[3].Value.Trim(), Visible(names, scopes, match.Index), catalog);
            var scope = match.Groups[1].Success ? null : Innermost(scopes, match.Index);
            if (scope == null)
            {
                names[name] = type;
            }
            else
            {
                ((Dictionary<string, TypeRef>)scope.Names)[name] = type;
            }
        }

        names.Remove("");
        return new DocumentBindings
        {
            Names = names,
            BufferSymbols = buffer,
            Scopes = scopes
        };
    }

    private static List<BindingScope> FunctionScopes(string joined, IReadOnlyList<AviSynthFunctionSpan> spans)
    {
        var scopes = new List<BindingScope>(spans.Count);
        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];
            var brace = span.ParenClose + 1;
            while (brace < joined.Length && char.IsWhiteSpace(joined[brace]))
            {
                brace++;
            }

            var hasBody = brace < joined.Length && joined[brace] == '{';
            var headerEnd = hasBody ? brace : span.ParenClose + 1;
            var closed = hasBody ? FunctionHeaders.MatchingBrace(joined, brace) : -1;
            var end = closed >= 0
                ? closed
                : i + 1 < spans.Count
                    ? spans[i + 1].Start - 1
                    : joined.Length;
            if (headerEnd > end)
            {
                headerEnd = end;
            }
            var names = new Dictionary<string, TypeRef>(StringComparer.OrdinalIgnoreCase);
            BindParameters(span.Symbol, names);
            scopes.Add(new BindingScope
            {
                Start = span.Start,
                End = end,
                Name = span.Symbol.Name,
                HeaderEnd = headerEnd,
                Names = names
            });
        }

        return scopes;
    }

    private static void BindParameters(Symbol symbol, Dictionary<string, TypeRef> names)
    {
        if (symbol.Parameters == null)
        {
            return;
        }

        foreach (var parameter in symbol.Parameters)
        {
            var name = ParameterNames.OfAviSynth(parameter);
            if (name == null)
            {
                continue;
            }

            var trimmed = parameter.Trim();
            if (name.Equals(trimmed, StringComparison.OrdinalIgnoreCase) && AviSynthTypes.IsTypeName(name))
            {
                continue;
            }

            var type = ParameterType(trimmed);
            if (type != null)
            {
                names[name] = type.Value;
            }
        }
    }

    private static TypeRef? ParameterType(string parameter)
    {
        var i = 0;
        while (i < parameter.Length && (char.IsLetter(parameter[i]) || parameter[i] == '_'))
        {
            i++;
        }

        if (i == 0)
        {
            return null;
        }

        var word = parameter[..i];
        if (word.Equals("clip", StringComparison.OrdinalIgnoreCase))
        {
            return AviSynthTypes.Clip;
        }

        if (word.Equals("int", StringComparison.OrdinalIgnoreCase))
        {
            return AviSynthTypes.Int;
        }

        if (word.Equals("float", StringComparison.OrdinalIgnoreCase))
        {
            return AviSynthTypes.Float;
        }

        if (word.Equals("bool", StringComparison.OrdinalIgnoreCase))
        {
            return AviSynthTypes.Bool;
        }

        if (word.Equals("string", StringComparison.OrdinalIgnoreCase))
        {
            return AviSynthTypes.String;
        }

        return null;
    }

    private static Dictionary<string, TypeRef> Visible(Dictionary<string, TypeRef> global,
        IReadOnlyList<BindingScope> scopes, int offset)
    {
        var names = new Dictionary<string, TypeRef>(global, global.Comparer);
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

        return names;
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

    private static TypeRef Infer(string expression, Dictionary<string, TypeRef> names, IReadOnlyList<Symbol> catalog)
    {
        var trimmed = ExpressionParts.UnwrapParentheses(expression);
        if (trimmed.StartsWith("Default(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(')'))
        {
            var inner = trimmed[8..^1];
            var args = ParameterNames.Split(inner);
            if (args.Length > 0)
            {
                var first = Infer(args[0], names, catalog);
                if (!first.IsUnknown)
                {
                    return first;
                }

                if (args.Length > 1)
                {
                    return Infer(args[1], names, catalog);
                }
            }
        }

        var question = ExpressionParts.IndexOutsideBrackets(trimmed, '?');
        if (question >= 0)
        {
            var rest = trimmed[(question + 1)..];
            var colon = ExpressionParts.IndexOutsideBrackets(rest, ':');
            if (colon >= 0)
            {
                var whenTrue = Infer(rest[..colon].Trim(), names, catalog);
                var whenFalse = Infer(rest[(colon + 1)..].Trim(), names, catalog);
                if (whenTrue == AviSynthTypes.Clip || whenFalse == AviSynthTypes.Clip)
                {
                    return AviSynthTypes.Clip;
                }

                if (!whenTrue.IsUnknown && whenTrue == whenFalse)
                {
                    return whenTrue;
                }

                return whenFalse.IsUnknown ? whenTrue : whenFalse;
            }
        }

        var parts = ExpressionParts.SplitAddMul(trimmed);
        if (parts.Count == 1)
        {
            return InferPart(expression, names, catalog);
        }

        var clip = TypeRef.Unknown;
        var last = TypeRef.Unknown;
        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                continue;
            }

            last = InferPart(part, names, catalog);
            if (last == AviSynthTypes.Clip)
            {
                clip = last;
            }
        }

        if (!clip.IsUnknown)
        {
            return clip;
        }

        return TypeRef.Unknown;
    }

    private static TypeRef InferPart(string part, Dictionary<string, TypeRef> names, IReadOnlyList<Symbol> catalog)
    {
        var segments = ExpressionReader.Parse(part);
        if (segments.Count == 0)
        {
            return TypeRef.Unknown;
        }

        return AviSynthTypeWalker.TypeOf(segments, new DocumentBindings { Names = names }, catalog);
    }
}
