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
        IReadOnlyList<Symbol> catalog, CancellationToken token, string? source = null)
    {
        var comparer = language.Comparison == StringComparison.OrdinalIgnoreCase
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var stack = new Stack<CallFrame>();
        for (var i = 0; i < code.Length; i++)
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

        var nested = false;
        foreach (var frame in stack)
        {
            if (frame.Delimiter != '(')
            {
                nested = true;
                continue;
            }

            var callee = ExpressionReader.Callee(code, frame.Offset);
            if (callee.Count == 0)
            {
                nested = true;
                continue;
            }

            var resolved = language.ResolveCall(callee, bindings, catalog);
            if (resolved is { Overloads.Count: > 0 })
            {
                var current = (source ?? code)[frame.ArgumentStart..];
                var parameter = NamedVisibleIndex(resolved, current, language) ?? frame.Parameter;
                return new CallScan(resolved.Overloads, parameter, resolved.ImplicitReceiver, nested,
                    current, frame.UsedNames, frame.Positional);
            }

            if (callee[^1].Name.Length > 0)
            {
                return null;
            }

            nested = true;
        }

        return null;
    }

    /// <summary>
    /// Gets the innermost unclosed <c>(</c>, <c>[</c>, or <c>{</c>, or null when all are balanced.
    /// </summary>
    public static char? InnermostUnclosed(string code, CancellationToken token = default,
        ILanguage? language = null)
    {
        var stack = new Stack<char>();
        for (var i = 0; i < code.Length; i++)
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

    private static int? NamedVisibleIndex(CallResolution resolved, string argument, ILanguage language)
    {
        var eq = ParameterNames.TopLevelKeywordEquals(argument);
        if (eq <= 0)
        {
            return null;
        }

        var name = argument[..eq].Trim();
        if (name.Length == 0)
        {
            return null;
        }

        var skip = resolved.ImplicitReceiver ? 1 : 0;
        foreach (var overload in resolved.Overloads)
        {
            if (overload.Parameters == null)
            {
                continue;
            }

            var visible = 0;
            foreach (var parameter in overload.Parameters)
            {
                if (ParameterNames.IsSeparator(parameter))
                {
                    continue;
                }

                var parameterName = language.ParameterName(parameter);
                if (parameterName != null &&
                    (parameterName.Equals(name, language.Comparison) ||
                     parameterName.Equals(name + "_", language.Comparison)))
                {
                    var mapped = visible - skip;
                    return mapped >= 0 ? mapped : null;
                }

                visible++;
            }
        }

        return null;
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
/// Resolved call plus internal argument-scan context.
/// </summary>
internal sealed record CallScan(
    IReadOnlyList<Symbol> Overloads,
    int ActiveParameter,
    bool ImplicitClip,
    bool InNestedDelimiter,
    string CurrentArgument,
    IReadOnlySet<string> UsedNames,
    int PositionalConsumed)
{
    /// <summary>
    /// Gets the consumer-facing insight.
    /// </summary>
    public CallInsight Insight => new(Overloads, ActiveParameter, ImplicitClip);
}
