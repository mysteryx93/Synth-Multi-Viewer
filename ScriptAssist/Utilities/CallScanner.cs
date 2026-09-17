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
                var parameter = NamedVisibleIndex(resolved, code[frame.ArgumentStart..], language);
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

            for (var i = 0; i < overload.Parameters.Length; i++)
            {
                var parameterName = language.ParameterName(overload.Parameters[i]);
                if (parameterName == null || !parameterName.Equals(name, language.Comparison))
                {
                    continue;
                }

                var visible = i - skip;
                return visible >= 0 ? visible : null;
            }
        }

        return null;
    }

    private readonly record struct CallFrame(char Delimiter, int Offset, int Parameter, int ArgumentStart);
}

/// <summary>
/// Reads named-argument position inside the innermost unclosed call.
/// </summary>
internal static class CallArguments
{
    /// <summary>
    /// Gets whether the caret sits in a <c>name=value</c> value.
    /// </summary>
    public static bool InValue(string prefix)
    {
        if (!TryInside(prefix, out var inside))
        {
            return false;
        }

        var current = LastArgument(inside);
        return ParameterNames.KeywordEqualsIndex(current) >= 0;
    }

    /// <summary>
    /// Gets keyword argument names already present in the innermost call.
    /// </summary>
    public static HashSet<string> UsedNames(string prefix, StringComparison comparison)
    {
        var used = new HashSet<string>(comparison == StringComparison.OrdinalIgnoreCase
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);
        if (!TryInside(prefix, out var inside))
        {
            return used;
        }

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

    private static bool TryInside(string prefix, out string inside)
    {
        inside = "";
        var stack = new Stack<int>();
        var other = 0;
        for (var i = 0; i < prefix.Length; i++)
        {
            var c = prefix[i];
            if (c is '[' or '{')
            {
                other++;
            }
            else if (c is ']' or '}' && other > 0)
            {
                other--;
            }
            else if (c == '(')
            {
                stack.Push(i);
            }
            else if (c == ')' && stack.Count > 0)
            {
                stack.Pop();
            }
        }

        if (stack.Count == 0)
        {
            return false;
        }

        inside = prefix[(stack.Peek() + 1)..];
        return true;
    }

    private static string LastArgument(string inside)
    {
        var start = 0;
        var depth = 0;
        var quote = '\0';
        for (var i = 0; i < inside.Length; i++)
        {
            var c = inside[i];
            if (quote != '\0')
            {
                if (c == '\\' && i + 1 < inside.Length)
                {
                    i++;
                    continue;
                }

                if (c == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}' && depth > 0)
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                start = i + 1;
            }
        }

        return inside[start..].Trim();
    }
}
