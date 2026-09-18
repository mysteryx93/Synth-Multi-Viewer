namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Finds the innermost unclosed call and its comma index.
/// </summary>
internal static class CallScanner
{
    /// <summary>
    /// Walks <paramref name="code"/> and returns insight when the language resolves the callee.
    /// </summary>
    public static CallScan? Find(string code, ILanguage language, DocumentBindings bindings,
        IReadOnlyList<Symbol> catalog, CancellationToken token, string? source = null, int caret = -1,
        bool[]? joins = null) =>
        Walk(code, language, bindings, catalog, token, source, caret, joins).Scan;

    /// <summary>
    /// Walks delimiters once and returns both the resolved call and the innermost unclosed delimiter.
    /// </summary>
    public static CallWalk Walk(string code, ILanguage language, DocumentBindings bindings,
        IReadOnlyList<Symbol> catalog, CancellationToken token, string? source = null, int caret = -1,
        bool[]? joins = null)
    {
        if (caret < 0 || caret > code.Length)
        {
            caret = code.Length;
        }

        var comparer = language.Comparison == StringComparison.OrdinalIgnoreCase
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        joins ??= StatementScanner.Joins(code, language, token);
        var stack = new Stack<CallFrame>();
        for (var i = 0; i < caret; i++)
        {
            if ((i & 4095) == 0)
            {
                token.ThrowIfCancellationRequested();
            }

            var c = code[i];
            if (c is '(' or '[' or '{')
            {
                stack.Push(new CallFrame(c, i, i + 1, comparer));
            }
            else if (c is ')' or ']' or '}')
            {
                CloseFrame(stack, c);
            }
            else if (c == ',' && stack.Count > 0)
            {
                stack.Peek().Comma(i + 1);
            }
            else if (c == '=' && stack.Count > 0 && ParameterNames.IsKeywordAssign(code, i))
            {
                stack.Peek().Keyword(source ?? code, i);
            }
            else if (c is '\n' or '\r')
            {
                var last = c == '\r' && i + 1 < code.Length && code[i + 1] == '\n' ? i + 1 : i;
                if (!StatementScanner.Continues(code, i) && StatementScanner.Recovers(code, last + 1, language))
                {
                    stack.Clear();
                }

                i = last;
            }
        }

        var unclosed = stack.Count == 0 ? (char?)null : stack.Peek().Delimiter;
        var nested = false;
        foreach (var frame in stack)
        {
            if (frame.Delimiter != '(')
            {
                nested = true;
                continue;
            }

            var callee = ExpressionReader.Callee(code, frame.Offset, language, token, joins);
            if (callee.Count == 0)
            {
                nested = true;
                continue;
            }

            var resolved = language.ResolveCall(callee, bindings, catalog);
            if (resolved is { Overloads.Count: > 0 })
            {
                var current = (source ?? code)[frame.ArgumentStart..caret];
                var used = CanonicalNames(frame.UsedNames, resolved, language);
                var keyword = KeywordName(current);
                var parameter = ActivePhysical(resolved.Overloads, keyword, frame.Positional,
                    resolved.ImplicitReceiver, used, language);
                return new CallWalk(new CallScan(resolved.Overloads, parameter, resolved.ImplicitReceiver, nested,
                    current, used, frame.Positional, keyword), unclosed);
            }

            if (callee[^1].Name.Length > 0)
            {
                return new CallWalk(null, unclosed);
            }

            nested = true;
        }

        return new CallWalk(null, unclosed);
    }

    /// <summary>
    /// Gets the innermost unclosed delimiter after walking <paramref name="code"/> to <paramref name="caret"/>.
    /// </summary>
    public static char? InnermostUnclosed(string code, CancellationToken token = default,
        ILanguage? language = null, int caret = -1)
    {
        if (caret < 0 || caret > code.Length)
        {
            caret = code.Length;
        }

        var stack = new Stack<char>();
        for (var i = 0; i < caret; i++)
        {
            if ((i & 4095) == 0)
            {
                token.ThrowIfCancellationRequested();
            }

            var c = code[i];
            if (c is '(' or '[' or '{')
            {
                stack.Push(c);
            }
            else if (c is ')' or ']' or '}')
            {
                StatementScanner.Close(stack, c);
            }
            else if (c is '\n' or '\r')
            {
                var last = c == '\r' && i + 1 < code.Length && code[i + 1] == '\n' ? i + 1 : i;
                if (!StatementScanner.Continues(code, i) && StatementScanner.Recovers(code, last + 1, language))
                {
                    stack.Clear();
                }

                i = last;
            }
        }

        return stack.Count == 0 ? null : stack.Peek();
    }

    private static void CloseFrame(Stack<CallFrame> stack, char close)
    {
        var open = StatementScanner.Opening(close);
        while (stack.Count > 0 && stack.Peek().Delimiter != open)
        {
            stack.Pop();
        }

        if (stack.Count > 0)
        {
            stack.Pop();
        }
    }

    internal static int ActivePhysical(IReadOnlyList<Symbol> overloads, string? keyword, int positional,
        bool implicitClip, IReadOnlySet<string>? used, ILanguage? language, Symbol? overload = null)
    {
        var skip = implicitClip ? 1 : 0;
        if (overload != null)
        {
            return overload.Parameters == null
                ? positional + skip
                : MapOverload(overload, keyword, positional, skip, used, language);
        }

        foreach (var candidate in overloads)
        {
            if (candidate.Parameters == null)
            {
                continue;
            }

            return MapOverload(candidate, keyword, positional, skip, used, language);
        }

        return positional + skip;
    }

    private static int MapOverload(Symbol overload, string? keyword, int positional, int skip,
        IReadOnlySet<string>? used, ILanguage? language)
    {
        var nameOf = language != null ? language.ParameterName : NameOf(overload);
        var comparison = language?.Comparison ?? ComparisonOf(overload);
        return ParameterNames.MapActive(overload.Parameters!, keyword, positional, skip, used, nameOf, comparison,
            NativeAlias(overload));
    }

    internal static bool NativeAlias(Symbol overload) =>
        overload.Name.StartsWith("core.", StringComparison.Ordinal);

    private static Func<string, string?> NameOf(Symbol overload) =>
        AviSynth(overload) ? ParameterNames.OfAviSynth : ParameterNames.OfPython;

    private static StringComparison ComparisonOf(Symbol overload) =>
        AviSynth(overload) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static bool AviSynth(Symbol overload)
    {
        if (overload.Parameters == null)
        {
            return false;
        }

        foreach (var parameter in overload.Parameters)
        {
            if (parameter.Contains('[', StringComparison.Ordinal) || parameter.Contains('"', StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? KeywordName(string argument)
    {
        var eq = ParameterNames.TopLevelKeywordEquals(argument);
        if (eq <= 0)
        {
            return null;
        }

        var name = argument[..eq].Trim();
        return name.Length == 0 ? null : name;
    }

    private static IReadOnlySet<string> CanonicalNames(IReadOnlySet<string> used, CallResolution resolved,
        ILanguage language)
    {
        var names = new HashSet<string>(language.Comparison == StringComparison.OrdinalIgnoreCase
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);
        foreach (var written in used)
        {
            var matched = false;
            foreach (var overload in resolved.Overloads)
            {
                if (overload.Parameters == null)
                {
                    continue;
                }

                foreach (var parameter in overload.Parameters)
                {
                    var name = language.ParameterName(parameter);
                    if (name == null ||
                        !ParameterNames.ArgumentEquals(name, written, language.Comparison, NativeAlias(overload)))
                    {
                        continue;
                    }

                    names.Add(name);
                    matched = true;
                }
            }

            if (!matched)
            {
                names.Add(written);
            }
        }

        return names;
    }

    private sealed class CallFrame(char delimiter, int offset, int argumentStart, StringComparer comparer)
    {
        public char Delimiter { get; } = delimiter;
        public int Offset { get; } = offset;
        public int Parameter { get; private set; }
        public int ArgumentStart { get; private set; } = argumentStart;
        public int Positional { get; private set; }
        public HashSet<string> UsedNames { get; } = new(comparer);
        private bool _keyword;

        public void Comma(int nextStart)
        {
            if (!_keyword)
            {
                Positional++;
            }

            Parameter++;
            ArgumentStart = nextStart;
            _keyword = false;
        }

        public void Keyword(string code, int equals)
        {
            if (Delimiter != '(' || _keyword)
            {
                return;
            }

            if (equals < ArgumentStart || equals > code.Length)
            {
                return;
            }

            var name = code[ArgumentStart..equals].Trim();
            if (name.Length == 0)
            {
                return;
            }

            for (var i = 0; i < name.Length; i++)
            {
                if (!BufferLexer.IsIdentifier(name[i]) || i == 0 && char.IsDigit(name[i]))
                {
                    return;
                }
            }

            _keyword = true;
            UsedNames.Add(name);
        }
    }
}

/// <summary>
/// Delimiter walk plus an optional resolved call.
/// </summary>
internal readonly record struct CallWalk(CallScan? Scan, char? Unclosed);

/// <summary>
/// Resolved call plus internal argument-scan context.
/// </summary>
internal sealed record CallScan(
    IReadOnlyList<Symbol> Overloads,
    int ActiveParameter,
    bool ImplicitClip,
    bool InNestedDelimiter,
    string CurrentArgument,
    IReadOnlySet<string> UsedNames,
    int PositionalConsumed,
    string? Keyword)
{
    /// <summary>
    /// Gets the consumer-facing insight.
    /// </summary>
    public CallInsight Insight => new(Overloads, ActiveParameter, ImplicitClip)
    {
        Keyword = Keyword,
        Positional = PositionalConsumed,
        UsedNames = UsedNames
    };
}
