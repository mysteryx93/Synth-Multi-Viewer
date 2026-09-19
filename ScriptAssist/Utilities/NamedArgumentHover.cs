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
        StringComparison comparison, out string? parameter, HoverContext? context)
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

        if (context?.Scan == null)
        {
            return context?.Unclosed == '(';
        }

        foreach (var overload in context.Scan.Overloads)
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
internal sealed class HoverContext(CallScan? scan, char? unclosed)
{
    public CallScan? Scan { get; } = scan;
    public char? Unclosed { get; } = unclosed;
}

/// <summary>
/// Hover that can reuse the request's already-computed call scan.
/// </summary>
internal interface IContextHover
{
    HoverInfo? Hover(string code, CaretPath path, DocumentBindings bindings, IReadOnlyList<Symbol> catalog,
        HoverContext? context);
}
