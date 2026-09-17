namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Resolves <c>name=</c> inside a call to the enclosing parameter, not a same-named property.
/// </summary>
internal static class NamedArgumentHover
{
    /// <summary>
    /// Returns true when the identifier is a keyword argument. <paramref name="parameter"/> is the
    /// matching catalog string; a match-less <c>name=</c> still consumes the token.
    /// </summary>
    public static bool TryGet(string code, CaretPath path, string name, ILanguage language,
        DocumentBindings bindings, IReadOnlyList<Symbol> catalog, StringComparison comparison,
        out string? parameter)
    {
        parameter = null;
        var i = path.End;
        while (i < code.Length && char.IsWhiteSpace(code[i]))
        {
            i++;
        }

        if (i >= code.Length || code[i] != '=' || i + 1 < code.Length && code[i + 1] == '=')
        {
            return false;
        }

        var prefix = code[..path.End];
        var insight = CallScanner.Find(prefix, language, bindings, catalog, CancellationToken.None);
        if (insight == null)
        {
            return CallScanner.InnermostUnclosed(prefix) == '(';
        }

        foreach (var overload in insight.Overloads)
        {
            if (overload.Parameters == null)
            {
                continue;
            }

            foreach (var candidate in overload.Parameters)
            {
                var parameterName = language.ParameterName(candidate);
                if (parameterName != null && parameterName.Equals(name, comparison))
                {
                    parameter = candidate;
                    return true;
                }
            }
        }

        return true;
    }
}
