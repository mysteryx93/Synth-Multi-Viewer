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
    public static CaretPath Read(string code, int caret)
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
        return new CaretPath
        {
            Start = start,
            End = end,
            Typed = typed,
            Segments = WalkLeft(code, start, out _)
        };
    }

    /// <summary>
    /// Parses a whole expression, including a trailing identifier, call, or index.
    /// Returns an empty path when the expression is not fully consumed.
    /// </summary>
    public static IReadOnlyList<PathSegment> Parse(string code)
    {
        if (!code.HasText())
        {
            return [];
        }

        var structure = BufferLexer.Mask(code, StructureLexer).Code;
        var trimmed = ExpressionParts.UnwrapParentheses(structure);
        if (trimmed.Length == 0)
        {
            return [];
        }

        IReadOnlyList<PathSegment> segments;
        int remaining;
        if (trimmed[^1] is ')' or ']')
        {
            if (!TryParseClosed(trimmed, out segments, out remaining))
            {
                return [];
            }
        }
        else
        {
            var path = Read(trimmed, trimmed.Length);
            if (path.Typed.Length == 0)
            {
                segments = path.Segments;
            }
            else
            {
                var list = new List<PathSegment>(path.Segments.Count + 1);
                list.AddRange(path.Segments);
                list.Add(new PathSegment { Name = path.Typed, Kind = PathSegmentKind.Name });
                segments = list;
            }

            WalkLeft(trimmed, path.Start, out remaining);
        }

        return Leftover(trimmed, remaining) ? [] : segments;
    }

    private static bool TryParseClosed(string code, out IReadOnlyList<PathSegment> segments, out int remaining)
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

        var prefix = WalkLeft(code, pos, out remaining);
        var list = new List<PathSegment>(prefix.Count + uses.Count);
        list.AddRange(prefix);
        AppendUses(list, name, uses, reverse: false);
        segments = list;
        return true;
    }

    /// <summary>
    /// Reads the callee to the left of an opening parenthesis.
    /// </summary>
    public static IReadOnlyList<PathSegment> Callee(string code, int openParen)
    {
        var end = openParen.Clamp(0, code.Length);
        while (end > 0 && char.IsWhiteSpace(code[end - 1]))
        {
            end--;
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
        var prefix = WalkLeft(code, start, out _);
        var segments = new List<PathSegment>(prefix.Count + 1);
        segments.AddRange(prefix);
        segments.Add(new PathSegment { Name = name, Kind = PathSegmentKind.Name });
        return segments;
    }

    private static IReadOnlyList<PathSegment> WalkLeft(string code, int position, out int remaining)
    {
        var collected = new List<PathSegment>();
        var pos = position;
        while (pos > 0)
        {
            if (!SkipJoin(code, ref pos))
            {
                break;
            }

            if (pos == 0 || code[pos - 1] != '.')
            {
                break;
            }

            pos--;
            if (!SkipJoin(code, ref pos))
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

                var inner = Parse(code[(open + 1)..close]);
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

                collected.Add(new PathSegment { Name = code[pos..nameEnd], Kind = PathSegmentKind.Name });
                continue;
            }

            break;
        }

        remaining = pos;
        collected.Reverse();
        return collected;
    }

    private static bool SkipJoin(string code, ref int pos)
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
        if (!StatementScanner.Continues(code, newline) && Depth(code, newline) == 0)
        {
            pos = saved;
            return false;
        }

        pos = newline;
        if (pos > 0 && code[pos - 1] == '\\')
        {
            pos--;
        }

        return SkipJoin(code, ref pos);
    }

    private static int Depth(string code, int end)
    {
        var depth = 0;
        for (var i = 0; i < end && i < code.Length; i++)
        {
            var c = code[i];
            if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}' && depth > 0)
            {
                depth--;
            }
        }

        return depth;
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
                segments.Add(new PathSegment { Name = i == 0 ? name : "", Kind = uses[i] });
            }

            return;
        }

        for (var i = 0; i < uses.Count; i++)
        {
            segments.Add(new PathSegment { Name = i == 0 ? name : "", Kind = uses[i] });
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
