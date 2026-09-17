namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Reads dotted, called, and indexed expressions while skipping balanced <c>()</c> and <c>[]</c>.
/// </summary>
internal static class ExpressionReader
{
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
            Segments = WalkLeft(code, start)
        };
    }

    /// <summary>
    /// Parses a whole expression, including a trailing identifier, call, or index.
    /// </summary>
    public static IReadOnlyList<PathSegment> Parse(string code)
    {
        if (!code.HasText()) { return []; }

        var trimmed = ExpressionParts.UnwrapParentheses(code);
        if (trimmed.Length > 0 && trimmed[^1] is ')' or ']')
        {
            return ParseClosed(trimmed);
        }

        var path = Read(trimmed, trimmed.Length);
        if (path.Typed.Length == 0)
        {
            return path.Segments;
        }

        var segments = new List<PathSegment>(path.Segments.Count + 1);
        segments.AddRange(path.Segments);
        segments.Add(new PathSegment { Name = path.Typed, Kind = PathSegmentKind.Name });
        return segments;
    }

    private static IReadOnlyList<PathSegment> ParseClosed(string code)
    {
        var pos = code.Length;
        while (pos > 0 && char.IsWhiteSpace(code[pos - 1]))
        {
            pos--;
        }

        if (pos == 0 || code[pos - 1] is not (')' or ']'))
        {
            return [];
        }

        if (!TryReadPostfix(code, ref pos, out var name, out var uses))
        {
            return [];
        }

        var prefix = WalkLeft(code, pos);
        var segments = new List<PathSegment>(prefix.Count + uses.Count);
        segments.AddRange(prefix);
        AppendUses(segments, name, uses, reverse: false);
        return segments;
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
        var prefix = WalkLeft(code, start);
        var segments = new List<PathSegment>(prefix.Count + 1);
        segments.AddRange(prefix);
        segments.Add(new PathSegment { Name = name, Kind = PathSegmentKind.Name });
        return segments;
    }

    private static IReadOnlyList<PathSegment> WalkLeft(string code, int position)
    {
        var collected = new List<PathSegment>();
        var pos = position;
        while (pos > 0)
        {
            while (pos > 0 && char.IsWhiteSpace(code[pos - 1]))
            {
                pos--;
            }

            if (pos == 0 || code[pos - 1] != '.')
            {
                break;
            }

            pos--;
            while (pos > 0 && char.IsWhiteSpace(code[pos - 1]))
            {
                pos--;
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

        collected.Reverse();
        return collected;
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
