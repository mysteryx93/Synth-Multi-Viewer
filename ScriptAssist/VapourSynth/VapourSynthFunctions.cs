using System.Text.RegularExpressions;

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
        var spans = new List<Symbol>();
        foreach (var span in Spans(text, lexer, token))
        {
            spans.Add(span.Symbol);
        }

        return spans;
    }

    /// <summary>
    /// Returns column-0 function headers with offsets; comments are ignored, strings kept for defaults.
    /// </summary>
    internal static IReadOnlyList<VapourSynthFunctionSpan> Spans(string text, LexerOptions lexer,
        CancellationToken token = default)
    {
        var clean = BufferLexer.Mask(text, lexer, token: token).Code;
        var quoted = BufferLexer.Mask(text, lexer, maskStrings: false, token: token).Code;
        var buffer = new List<VapourSynthFunctionSpan>();
        var matches = VapourSynthPatterns.TopLevelDef().Matches(clean);
        for (var i = 0; i < matches.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var match = matches[i];
            var open = match.Index + match.Length - 1;
            var limit = i + 1 < matches.Count ? matches[i + 1].Index : clean.Length;
            var close = FunctionHeaders.MatchingClose(clean, open, limit, token);
            if (close < 0)
            {
                continue;
            }

            var parameters = ParameterNames.Split(quoted[(open + 1)..close]);
            var returnType = ReturnId(quoted, close);
            buffer.Add(new VapourSynthFunctionSpan(
                new Symbol(match.Groups[1].Value, parameters, ReturnType: returnType),
                match.Index, close));
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
}

/// <summary>
/// A column-0 <c>def</c> header and the offset of its closing <c>)</c>.
/// </summary>
internal readonly record struct VapourSynthFunctionSpan(Symbol Symbol, int Start, int ParenClose);
