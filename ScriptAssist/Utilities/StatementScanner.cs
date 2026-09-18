namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Splits a masked buffer into logical statements while tracking brackets and line continuations.
/// Offsets match the original document.
/// </summary>
internal static class StatementScanner
{
    /// <summary>
    /// A half-open statement span in UTF-16 offsets.
    /// </summary>
    internal readonly record struct Span(int Start, int End);

    /// <summary>
    /// Returns non-empty statements in <paramref name="code"/>. Strings and comments must already be masked.
    /// </summary>
    public static IReadOnlyList<Span> Scan(string code, CancellationToken token = default)
    {
        var spans = new List<Span>();
        var start = 0;
        var depth = 0;
        var i = 0;
        while (i < code.Length)
        {
            if ((i & 4095) == 0)
            {
                token.ThrowIfCancellationRequested();
            }

            var c = code[i];
            if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}' && depth > 0)
            {
                depth--;
            }
            else if (c == ';' && depth == 0)
            {
                Add(spans, code, start, i);
                start = i + 1;
            }
            else if (c == '\n' || c == '\r')
            {
                var last = c == '\r' && i + 1 < code.Length && code[i + 1] == '\n' ? i + 1 : i;
                if (!Continues(code, i))
                {
                    if (depth == 0 || Recovers(code, last + 1))
                    {
                        Add(spans, code, start, i);
                        start = last + 1;
                        depth = 0;
                    }
                }

                i = last;
            }

            i++;
        }

        Add(spans, code, start, code.Length);
        return spans;
    }

    /// <summary>
    /// Gets whether a line at <paramref name="lineStart"/> begins a <c>def</c>, <c>class</c>, or
    /// <c>function</c> declaration after indentation.
    /// </summary>
    public static bool Recovers(string code, int lineStart)
    {
        if (lineStart < 0 || lineStart >= code.Length)
        {
            return false;
        }

        var i = lineStart;
        while (i < code.Length && code[i] is ' ' or '\t')
        {
            i++;
        }

        return StartsKeyword(code, i, "def") || StartsKeyword(code, i, "class") ||
            StartsKeyword(code, i, "function");
    }

    /// <summary>
    /// Gets whether <paramref name="newline"/> is a backslash line continuation.
    /// </summary>
    public static bool Continues(string code, int newline)
    {
        var i = newline;
        while (i > 0 && code[i - 1] is ' ' or '\t')
        {
            i--;
        }

        return i > 0 && code[i - 1] == '\\';
    }

    /// <summary>
    /// Maps a closer to its opener, or <c>\0</c>.
    /// </summary>
    public static char Opening(char close) => close switch
    {
        ')' => '(',
        ']' => '[',
        '}' => '{',
        _ => '\0'
    };

    private static bool StartsKeyword(string code, int offset, string word)
    {
        if (offset < 0 || offset + word.Length > code.Length)
        {
            return false;
        }

        if (!code.AsSpan(offset, word.Length).Equals(word, StringComparison.Ordinal))
        {
            return false;
        }

        var after = offset + word.Length;
        return after == code.Length || !BufferLexer.IsIdentifier(code[after]);
    }

    private static void Add(List<Span> spans, string code, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(code[start]))
        {
            start++;
        }

        if (end > start)
        {
            spans.Add(new Span(start, end));
        }
    }
}
