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
            else if (depth == 0 && (c == '\n' || c == '\r'))
            {
                var last = c == '\r' && i + 1 < code.Length && code[i + 1] == '\n' ? i + 1 : i;
                if (!Continues(code, i))
                {
                    Add(spans, code, start, i);
                    start = last + 1;
                }

                i = last;
            }

            i++;
        }

        Add(spans, code, start, code.Length);
        return spans;
    }

    private static bool Continues(string code, int newline)
    {
        var i = newline;
        while (i > 0 && code[i - 1] is ' ' or '\t')
        {
            i--;
        }

        return i > 0 && code[i - 1] == '\\';
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
