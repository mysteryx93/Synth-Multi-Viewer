namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Parses top-level Python <c>def Name(...)</c> headers from script text.
/// </summary>
public static class VapourSynthFunctions
{
    /// <summary>
    /// Returns column-0 function definitions; nested defs are ignored.
    /// </summary>
    public static IReadOnlyList<Symbol> Parse(string text, LexerOptions lexer, CancellationToken token = default)
    {
        var clean = BufferLexer.Mask(text, lexer, token: token).Code;
        var quoted = BufferLexer.Mask(text, lexer, maskStrings: false, token: token).Code;
        var buffer = new List<Symbol>();
        var statements = StatementScanner.Scan(clean, token);
        for (var i = 0; i < statements.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var span = statements[i];
            if (!ColumnZero(clean, span.Start) ||
                !PythonHeaders.TryDef(quoted, span.Start, span.End, out var name, out var open, out var async))
            {
                continue;
            }

            var close = FunctionHeaders.MatchingClose(clean, open, span.End, token);
            if (close < 0)
            {
                continue;
            }

            var parameters = ParameterNames.Split(quoted[(open + 1)..close]);
            var returnType = async ? null : ReturnId(quoted, close);
            buffer.Add(new(name, parameters, ReturnType: returnType));
        }

        return buffer;
    }

    internal static string? ReturnId(string text, int parenClose, DocumentBindings? bindings = null)
    {
        var i = parenClose + 1;
        while (i < text.Length && text[i] is ' ' or '\t')
        {
            i++;
        }

        if (i + 1 >= text.Length || text[i] != '-' || text[i + 1] != '>')
        {
            return null;
        }

        i += 2;
        var start = i;
        while (i < text.Length && text[i] is not ':' and not '\n' and not '\r')
        {
            i++;
        }

        return VapourSynthBinder.ResolveAnnotationId(text[start..i].Trim(), bindings);
    }

    private static bool ColumnZero(string text, int offset) =>
        offset == 0 || text[offset - 1] is '\n' or '\r';
}
