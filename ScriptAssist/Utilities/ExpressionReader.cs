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

        var trimmed = code.TrimEnd();
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
        var closer = code[^1];
        var opener = closer == ')' ? '(' : '[';
        var kind = closer == ')' ? PathSegmentKind.Call : PathSegmentKind.Index;
        var pos = SkipBalanced(code, code.Length - 1, closer, opener);
        while (pos > 0 && char.IsWhiteSpace(code[pos - 1]))
        {
            pos--;
        }

        var nameEnd = pos;
        while (pos > 0 && BufferLexer.IsIdentifier(code[pos - 1]))
        {
            pos--;
        }

        if (pos == nameEnd)
        {
            return [];
        }

        var prefix = WalkLeft(code, pos);
        var segments = new List<PathSegment>(prefix.Count + 1);
        segments.AddRange(prefix);
        segments.Add(new PathSegment { Name = code[pos..nameEnd], Kind = kind });
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
            if (code[pos - 1] != '.')
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
                var closer = code[pos - 1];
                var opener = closer == ')' ? '(' : '[';
                var kind = closer == ')' ? PathSegmentKind.Call : PathSegmentKind.Index;
                pos = SkipBalanced(code, pos - 1, closer, opener);
                while (pos > 0 && char.IsWhiteSpace(code[pos - 1]))
                {
                    pos--;
                }

                var nameEnd = pos;
                while (pos > 0 && BufferLexer.IsIdentifier(code[pos - 1]))
                {
                    pos--;
                }

                if (pos == nameEnd)
                {
                    break;
                }

                collected.Add(new PathSegment { Name = code[pos..nameEnd], Kind = kind });
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
