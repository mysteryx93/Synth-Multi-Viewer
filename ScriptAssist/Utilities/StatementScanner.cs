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
        var stack = new Stack<char>();
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
                stack.Push(c);
            }
            else if (c is ')' or ']' or '}')
            {
                Close(stack, c);
            }
            else if (c == ';' && stack.Count == 0)
            {
                Add(spans, code, start, i);
                start = i + 1;
            }
            else if (c == '\n' || c == '\r')
            {
                var last = c == '\r' && i + 1 < code.Length && code[i + 1] == '\n' ? i + 1 : i;
                if (!Continues(code, i))
                {
                    if (stack.Count == 0 || Recovers(code, last + 1))
                    {
                        Add(spans, code, start, i);
                        start = last + 1;
                        stack.Clear();
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
    /// Marks newline starts that may be crossed as implicit or explicit continuations.
    /// </summary>
    public static bool[] Joins(string code, ILanguage? language = null, CancellationToken token = default)
    {
        var joins = new bool[code.Length];
        var stack = new Stack<char>();
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
                stack.Push(c);
            }
            else if (c is ')' or ']' or '}')
            {
                Close(stack, c);
            }
            else if (c == '\n' || c == '\r')
            {
                var last = c == '\r' && i + 1 < code.Length && code[i + 1] == '\n' ? i + 1 : i;
                if (Continues(code, i) || stack.Count > 0 && !Recovers(code, last + 1, language))
                {
                    joins[i] = true;
                }
                else
                {
                    stack.Clear();
                }

                i = last;
            }

            i++;
        }

        return joins;
    }

    /// <summary>
    /// Start of the logical statement containing <paramref name="caret"/>, using <paramref name="joins"/>.
    /// </summary>
    public static int StatementStart(string code, bool[] joins, int caret)
    {
        if (caret <= 0)
        {
            return 0;
        }

        var i = caret < code.Length ? caret : code.Length;
        while (i > 0)
        {
            i--;
            if (code[i] is not '\n' and not '\r')
            {
                continue;
            }

            var flag = i;
            if (code[i] == '\n' && i > 0 && code[i - 1] == '\r')
            {
                flag = i - 1;
            }

            if (flag < joins.Length && joins[flag])
            {
                continue;
            }

            return i + 1;
        }

        return 0;
    }

    /// <summary>
    /// Gets whether a line at <paramref name="lineStart"/> begins a language-specific declaration.
    /// Python recovers at <c>def</c>/<c>class</c> name prefixes; AviSynth at <c>function</c> name prefixes.
    /// </summary>
    public static bool Recovers(string code, int lineStart, ILanguage? language = null)
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

        var aviSynth = language?.Lexer.BackslashLineContinuations == true;
        if (aviSynth)
        {
            return Declaration(code, i, "function", StringComparison.OrdinalIgnoreCase);
        }

        return Declaration(code, i, "def", StringComparison.Ordinal) ||
            Declaration(code, i, "class", StringComparison.Ordinal);
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

    /// <summary>
    /// Pops mismatched openers until <paramref name="close"/> matches, then pops that opener.
    /// </summary>
    public static void Close(Stack<char> stack, char close)
    {
        var open = Opening(close);
        while (stack.Count > 0 && stack.Peek() != open)
        {
            stack.Pop();
        }

        if (stack.Count > 0)
        {
            stack.Pop();
        }
    }

    private static bool Declaration(string code, int offset, string keyword, StringComparison comparison)
    {
        if (!StartsKeyword(code, offset, keyword, comparison))
        {
            return false;
        }

        var i = offset + keyword.Length;
        while (i < code.Length && code[i] is ' ' or '\t')
        {
            i++;
        }

        if (i >= code.Length || !BufferLexer.IsIdentifier(code[i]) || char.IsDigit(code[i]))
        {
            return false;
        }

        i++;
        while (i < code.Length && BufferLexer.IsIdentifier(code[i]))
        {
            i++;
        }

        while (i < code.Length && code[i] is ' ' or '\t')
        {
            i++;
        }

        if (i >= code.Length || code[i] is '\n' or '\r')
        {
            return true;
        }

        if (code[i] == '(')
        {
            return true;
        }

        return keyword.Equals("class", StringComparison.Ordinal) && code[i] == ':';
    }

    private static bool StartsKeyword(string code, int offset, string word, StringComparison comparison)
    {
        if (offset < 0 || offset + word.Length > code.Length)
        {
            return false;
        }

        if (!code.AsSpan(offset, word.Length).Equals(word, comparison))
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
            spans.Add(new(start, end));
        }
    }
}
