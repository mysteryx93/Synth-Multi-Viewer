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
        IReadOnlyList<Symbol> catalog, CancellationToken token)
    {
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
                stack.Push(new CallFrame(c, i, 0, i + 1));
            }
            else if (c is ')' or ']' or '}' && stack.Count > 0)
            {
                stack.Pop();
            }
            else if (c == ',' && stack.Count > 0)
            {
                var frame = stack.Pop();
                stack.Push(new CallFrame(frame.Delimiter, frame.Offset, frame.Parameter + 1, i + 1));
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
                var argumentList = code[(frame.Offset + 1)..];
                var current = code[frame.ArgumentStart..];
                var parameter = NamedVisibleIndex(resolved, current, language) ?? frame.Parameter;
                return new CallScan(resolved.Overloads, parameter, resolved.ImplicitReceiver, nested, argumentList,
                    current, UsedNames(argumentList, language.Comparison));
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
    public static char? InnermostUnclosed(string code, CancellationToken token = default)
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
            else if (c is ')' or ']' or '}' && stack.Count > 0)
            {
                stack.Pop();
            }
        }

        return stack.Count == 0 ? null : stack.Peek();
    }

    private static int? NamedVisibleIndex(CallResolution resolved, string argument, ILanguage language)
    {
        var eq = ParameterNames.KeywordEqualsIndex(argument);
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
                if (parameterName != null && parameterName.Equals(name, language.Comparison))
                {
                    var mapped = visible - skip;
                    return mapped >= 0 ? mapped : null;
                }

                visible++;
            }
        }

        return null;
    }

    private static HashSet<string> UsedNames(string inside, StringComparison comparison)
    {
        var used = new HashSet<string>(comparison == StringComparison.OrdinalIgnoreCase
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);
        foreach (var part in ParameterNames.Split(inside))
        {
            var eq = ParameterNames.KeywordEqualsIndex(part);
            if (eq <= 0)
            {
                continue;
            }

            var name = part[..eq].Trim();
            if (name.Length > 0)
            {
                used.Add(name);
            }
        }

        return used;
    }

    private readonly record struct CallFrame(char Delimiter, int Offset, int Parameter, int ArgumentStart);
}

/// <summary>
/// Resolved call plus internal argument-scan context.
/// </summary>
internal sealed record CallScan(
    IReadOnlyList<Symbol> Overloads,
    int ActiveParameter,
    bool ImplicitClip,
    bool InNestedDelimiter,
    string ArgumentList,
    string CurrentArgument,
    IReadOnlySet<string> UsedNames)
{
    /// <summary>
    /// Gets the consumer-facing insight.
    /// </summary>
    public CallInsight Insight => new(Overloads, ActiveParameter, ImplicitClip);
}
