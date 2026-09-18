namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Resolves <c>name=</c> inside a call to the enclosing parameter, not a same-named property.
/// </summary>
internal static class NamedArgumentHover
{
    [ThreadStatic]
    private static HoverContext? t_context;

    /// <summary>
    /// Reuses the request's call scan, continuation map, and cancellation token.
    /// </summary>
    internal static void Bind(HoverContext context) => t_context = context;

    /// <summary>
    /// Clears the request-scoped hover context.
    /// </summary>
    internal static void Unbind() => t_context = null;

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

        var context = t_context;
        var insight = context != null
            ? context.Scan
            : CallScanner.Find(code, language, bindings, catalog, default, caret: path.End);
        if (insight == null)
        {
            return context != null
                ? context.Unclosed == '('
                : CallScanner.InnermostUnclosed(code, default, language, path.End) == '(';
        }

        foreach (var overload in insight.Overloads)
        {
            if (overload.Parameters == null)
            {
                continue;
            }

            var slot = ParameterNames.MapNamed(overload.Parameters, name, language.ParameterName, comparison,
                CallScanner.NativeAlias(overload));
            if ((uint)slot < (uint)overload.Parameters.Length)
            {
                parameter = overload.Parameters[slot];
                return true;
            }
        }

        return true;
    }
}

/// <summary>
/// Call information already computed for the current analysis request.
/// </summary>
internal sealed class HoverContext(CallScan? scan, char? unclosed, bool[]? joins, CancellationToken token)
{
    public CallScan? Scan { get; } = scan;
    public char? Unclosed { get; } = unclosed;
    public bool[]? Joins { get; } = joins;
    public CancellationToken Token { get; } = token;
}
