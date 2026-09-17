namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Finds the innermost unclosed call and its comma index.
/// </summary>
internal static class CallScanner
{
    /// <summary>
    /// Walks <paramref name="code"/> and returns insight when the language resolves the callee.
    /// </summary>
    public static CallInsight? Find(string code, ILanguage language, DocumentBindings bindings,
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
            else if (c is ')' or ']' or '}')
            {
                if (stack.Count > 0)
                {
                    stack.Pop();
                }
            }
            else if (c == ',' && stack.Count > 0)
            {
                var frame = stack.Pop();
                stack.Push(new CallFrame(frame.Delimiter, frame.Offset, frame.Parameter + 1, i + 1));
            }
        }

        foreach (var frame in stack)
        {
            if (frame.Delimiter != '(')
            {
                continue;
            }

            var callee = ExpressionReader.Callee(code, frame.Offset);
            if (callee.Count == 0)
            {
                continue;
            }

            var resolved = language.ResolveCall(callee, bindings, catalog);
            if (resolved is { Overloads.Count: > 0 })
            {
                var parameter = NamedVisibleIndex(resolved, code[frame.ArgumentStart..], language.Comparison);
                return new CallInsight(resolved.Overloads, parameter ?? frame.Parameter, resolved.ImplicitReceiver);
            }

            if (callee[^1].Name.Length > 0)
            {
                return null;
            }
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

    private static int? NamedVisibleIndex(CallResolution resolved, string argument, StringComparison comparison)
    {
        var eq = argument.IndexOf('=');
        if (eq <= 0)
        {
            return null;
        }

        var name = argument[..eq].Trim();
        if (name.Length == 0)
        {
            return null;
        }

        var parameters = resolved.Overloads[0].Parameters;
        if (parameters == null)
        {
            return null;
        }

        var skip = resolved.ImplicitReceiver ? 1 : 0;
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameterName = ParameterNames.Of(parameters[i]);
            if (parameterName == null || !parameterName.Equals(name, comparison))
            {
                continue;
            }

            var visible = i - skip;
            return visible >= 0 ? visible : null;
        }

        return null;
    }

    private readonly record struct CallFrame(char Delimiter, int Offset, int Parameter, int ArgumentStart);
}
