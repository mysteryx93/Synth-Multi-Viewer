namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Reads dotted, called, and indexed expressions while skipping balanced <c>()</c> and <c>[]</c>.
/// </summary>
internal static class ExpressionReader
{
    private static readonly LexerOptions StructureLexer = new()
    {
        HashLineComments = true,
        SingleQuotes = true,
        TripleQuotes = true,
        StringEscapes = true
    };

    /// <summary>
    /// Reads the path at <paramref name="caret"/> for completion replacement.
    /// </summary>
    public static CaretPath Read(string code, int caret, ILanguage? language = null,
        CancellationToken token = default, bool[]? joins = null)
    {
        caret = caret.Clamp(0, code.Length);
        var start = caret;
        while (start > 0 && BufferLexer.IsIdentifier(code[start - 1]))
        {
            start--;
        }

        var end = caret;
        while (end < code.Length && BufferLexer.IsIdentifier(code[end]))
        {
            end++;
        }

        var typed = start <= caret && caret <= code.Length ? code[start..caret] : "";
        joins ??= StatementScanner.Joins(code, language, token);
        return new()
        {
            Start = start,
            End = end,
            Typed = typed,
            Segments = WalkLeft(code, start, joins, 0, language, token, out _)
        };
    }

    /// <summary>
    /// Parses a whole expression, including a trailing identifier, call, or index.
    /// Returns an empty path when the expression is not fully consumed.
    /// </summary>
    public static IReadOnlyList<PathSegment> Parse(string code, ILanguage? language = null,
        CancellationToken token = default)
    {
        if (!code.HasText())
        {
            return [];
        }

        var structure = BufferLexer.Mask(code, StructureLexer, token: token).Code;
        var joins = StatementScanner.Joins(structure, language, token);
        var (trimmed, offset) = ExpressionParts.UnwrapSpan(structure);
        if (trimmed.Length == 0)
        {
            return [];
        }

        IReadOnlyList<PathSegment> segments;
        int remaining;
        if (trimmed[^1] is ')' or ']')
        {
            if (!TryParseClosed(trimmed, joins, offset, language, token, out segments, out remaining))
            {
                return [];
            }
        }
        else
        {
            var typedStart = trimmed.Length;
            while (typedStart > 0 && BufferLexer.IsIdentifier(trimmed[typedStart - 1]))
            {
                typedStart--;
            }

            var prefix = WalkLeft(trimmed, typedStart, joins, offset, language, token, out remaining);
            if (typedStart == trimmed.Length)
            {
                segments = prefix;
            }
            else
            {
                var list = new List<PathSegment>(prefix.Count + 1);
                list.AddRange(prefix);
                list.Add(new() { Name = trimmed[typedStart..], Kind = PathSegmentKind.Name });
                segments = list;
            }
        }

        return Leftover(trimmed, remaining) ? [] : segments;
    }

    private static bool TryParseClosed(string code, bool[] joins, int offset, ILanguage? language,
        CancellationToken token, out IReadOnlyList<PathSegment> segments, out int remaining)
    {
        segments = [];
        remaining = 0;
        var pos = code.Length;
        while (pos > 0 && char.IsWhiteSpace(code[pos - 1]))
        {
            pos--;
        }

        if (pos == 0 || code[pos - 1] is not (')' or ']'))
        {
            return false;
        }

        if (!TryReadPostfix(code, ref pos, out var name, out var uses))
        {
            return false;
        }

        var prefix = WalkLeft(code, pos, joins, offset, language, token, out remaining);
        var list = new List<PathSegment>(prefix.Count + uses.Count);
        list.AddRange(prefix);
        AppendUses(list, name, uses, reverse: false);
        segments = list;
        return true;
    }

    /// <summary>
    /// Reads the callee to the left of an opening parenthesis.
    /// </summary>
    public static IReadOnlyList<PathSegment> Callee(string code, int openParen, ILanguage? language = null,
        CancellationToken token = default, bool[]? joins = null)
    {
        var end = openParen.Clamp(0, code.Length);
        joins ??= StatementScanner.Joins(code, language, token);
        if (!SkipJoin(code, ref end, joins, 0))
        {
            return [];
        }

        var start = end;
        while (start > 0 && BufferLexer.IsIdentifier(code[start - 1]))
        {
            start--;
        }

        if (start == end)
        {
            return [];
        }

        var name = code[start..end];
        var prefix = WalkLeft(code, start, joins, 0, language, token, out _);
        var segments = new List<PathSegment>(prefix.Count + 1);
        segments.AddRange(prefix);
        segments.Add(new() { Name = name, Kind = PathSegmentKind.Name });
        return segments;
    }

    private static IReadOnlyList<PathSegment> WalkLeft(string code, int position, bool[] joins, int offset,
        ILanguage? language, CancellationToken token, out int remaining)
    {
        var collected = new List<PathSegment>();
        var pos = position;
        var steps = 0;
        while (pos > 0)
        {
            if ((steps++ & 4095) == 0)
            {
                token.ThrowIfCancellationRequested();
            }

            if (!SkipJoin(code, ref pos, joins, offset))
            {
                break;
            }

            if (pos == 0 || code[pos - 1] != '.')
            {
                break;
            }

            pos--;
            if (!SkipJoin(code, ref pos, joins, offset))
            {
                break;
            }

            if (pos > 0 && code[pos - 1] is ')' or ']')
            {
                var saved = pos;
                if (TryReadPostfix(code, ref pos, out var name, out var uses))
                {
                    AppendUses(collected, name, uses, reverse: true);
                    continue;
                }

                pos = saved;
                if (code[pos - 1] != ')')
                {
                    break;
                }

                var close = pos - 1;
                var open = SkipBalanced(code, close, ')', '(');
                if (open >= close)
                {
                    break;
                }

                var inner = Parse(code[open..(close + 1)], language, token);
                for (var i = inner.Count - 1; i >= 0; i--)
                {
                    collected.Add(inner[i]);
                }

                pos = open;
                continue;
            }

            if (pos > 0 && BufferLexer.IsIdentifier(code[pos - 1]))
            {
                var nameEnd = pos;
                while (pos > 0 && BufferLexer.IsIdentifier(code[pos - 1]))
                {
                    pos--;
                }

                collected.Add(new() { Name = code[pos..nameEnd], Kind = PathSegmentKind.Name });
                continue;
            }

            break;
        }

        remaining = pos;
        collected.Reverse();
        return collected;
    }

    private static bool SkipJoin(string code, ref int pos, bool[] joins, int offset)
    {
        var origin = pos;
        while (pos > 0)
        {
            var saved = pos;
            while (pos > 0 && code[pos - 1] is ' ' or '\t')
            {
                pos--;
            }

            if (pos == 0 || code[pos - 1] is not ('\n' or '\r'))
            {
                return true;
            }

            var last = pos - 1;
            var newline = code[last] == '\n' && last > 0 && code[last - 1] == '\r' ? last - 1 : last;
            var at = newline + offset;
            if (at < 0 || at >= joins.Length || !joins[at])
            {
                pos = saved;
                return saved != origin;
            }

            pos = newline;
            if (pos > 0 && code[pos - 1] == '\\')
            {
                pos--;
            }
        }

        return true;
    }

    private static bool Leftover(string code, int remaining)
    {
        for (var i = 0; i < remaining; i++)
        {
            if (!char.IsWhiteSpace(code[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadPostfix(string code, ref int pos, out string name, out List<PathSegmentKind> uses)
    {
        name = "";
        uses = [];
        while (pos > 0 && code[pos - 1] is ')' or ']')
        {
            var closer = code[pos - 1];
            var opener = closer == ')' ? '(' : '[';
            var kind = closer == ')' ? PathSegmentKind.Call : PathSegmentKind.Index;
            pos = SkipBalanced(code, pos - 1, closer, opener);
            while (pos > 0 && code[pos - 1] is ' ' or '\t')
            {
                pos--;
            }

            uses.Add(kind);
        }

        uses.Reverse();
        var nameEnd = pos;
        while (pos > 0 && BufferLexer.IsIdentifier(code[pos - 1]))
        {
            pos--;
        }

        if (pos == nameEnd || uses.Count == 0)
        {
            return false;
        }

        name = code[pos..nameEnd];
        return true;
    }

    private static void AppendUses(List<PathSegment> segments, string name, List<PathSegmentKind> uses, bool reverse)
    {
        if (reverse)
        {
            for (var i = uses.Count - 1; i >= 0; i--)
            {
                segments.Add(new() { Name = i == 0 ? name : "", Kind = uses[i] });
            }

            return;
        }

        for (var i = 0; i < uses.Count; i++)
        {
            segments.Add(new() { Name = i == 0 ? name : "", Kind = uses[i] });
        }
    }

    private static int SkipBalanced(string code, int closerIndex, char closer, char opener)
    {
        var depth = 1;
        var i = closerIndex;
        while (i > 0 && depth > 0)
        {
            i--;
            var c = code[i];
            if (c == closer)
            {
                depth++;
            }
            else if (c == opener)
            {
                depth--;
            }
        }

        return depth == 0 ? i : 0;
    }
}
