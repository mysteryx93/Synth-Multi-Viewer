namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Class body ranges and indent-based block ends for def/class headers.
/// </summary>
internal static class VapourSynthClassRanges
{
    public static List<(int Start, int End)> ClassRanges(string clean, string quoted,
        IReadOnlyList<StatementScanner.Span> statements)
    {
        var ranges = new List<(int Start, int End)>();
        for (var i = 0; i < statements.Count; i++)
        {
            var span = statements[i];
            if (!TryClass(quoted, clean, span, out var headerEnd))
            {
                continue;
            }

            ranges.Add((span.Start, BlockEnd(clean, statements, i, span, headerEnd)));
        }

        return ranges;
    }

    public static bool DirectlyInClass(int offset, IReadOnlyList<(int Start, int End)> classes,
        BindingScope? inner)
    {
        foreach (var range in classes)
        {
            if (offset <= range.Start || offset > range.End)
            {
                continue;
            }

            return inner == null || inner.Start <= range.Start;
        }

        return false;
    }

    public static bool EnclosedByClass(BindingScope self, IReadOnlyList<(int Start, int End)> classes)
    {
        var parent = self.Enclosing;
        foreach (var range in classes)
        {
            if (self.Start <= range.Start || self.Start > range.End)
            {
                continue;
            }

            return parent == null || range.Start >= parent.Start;
        }

        return false;
    }

    public static int HeaderColon(string text, int parenClose)
    {
        var i = parenClose + 1;
        while (i < text.Length && text[i] is ' ' or '\t')
        {
            i++;
        }

        if (i + 1 < text.Length && text[i] == '-' && text[i + 1] == '>')
        {
            i += 2;
            while (i < text.Length && text[i] is not ':' and not '\n' and not '\r')
            {
                i++;
            }
        }

        while (i < text.Length && text[i] is not ':' and not '\n' and not '\r')
        {
            i++;
        }

        return i < text.Length && text[i] == ':' ? i + 1 : parenClose + 1;
    }

    public static int BlockEnd(string text, IReadOnlyList<StatementScanner.Span> statements, int defIndex,
        StatementScanner.Span def, int headerEnd)
    {
        var i = headerEnd;
        while (i < def.End && i < text.Length && text[i] is ' ' or '\t')
        {
            i++;
        }

        if (i < def.End && i < text.Length && text[i] is not '\n' and not '\r' and not '#')
        {
            var line = LineStart(text, def.Start);
            var end = def.End;
            for (var n = defIndex + 1; n < statements.Count; n++)
            {
                if (LineStart(text, statements[n].Start) != line)
                {
                    break;
                }

                end = statements[n].End;
            }

            return end;
        }

        var defIndent = IndentAt(text, def.Start);
        var bodyIndent = -1;
        for (var n = defIndex + 1; n < statements.Count; n++)
        {
            var statement = statements[n];
            var indent = IndentAt(text, statement.Start);
            if (bodyIndent < 0)
            {
                if (indent <= defIndent)
                {
                    var start = LineStart(text, statement.Start);
                    return start == 0 ? headerEnd : start - 1;
                }

                bodyIndent = indent;
                continue;
            }

            if (indent < bodyIndent)
            {
                var start = LineStart(text, statement.Start);
                return start == 0 ? headerEnd : start - 1;
            }
        }

        return text.Length;
    }

    public static int IndentAt(string text, int offset) => LineIndent(text, LineStart(text, offset));

    private static bool TryClass(string quoted, string clean, StatementScanner.Span span, out int headerEnd)
    {
        headerEnd = span.Start;
        if (!VapourSynthBinder.Keyword(quoted, span.Start, span.End, "class"))
        {
            return false;
        }

        var i = VapourSynthBinder.AfterKeyword(quoted, span.Start, span.End, "class");
        if (!VapourSynthBinder.TryIdent(quoted, ref i, span.End, out _))
        {
            return false;
        }

        VapourSynthBinder.SkipWs(quoted, ref i, span.End);
        var close = i > 0 ? i - 1 : 0;
        if (i < span.End && quoted[i] == '(')
        {
            var match = FunctionHeaders.MatchingClose(clean, i, span.End);
            if (match < 0)
            {
                return false;
            }

            close = match;
        }

        headerEnd = HeaderColon(clean, close);
        return headerEnd > close;
    }

    private static int LineStart(string text, int offset)
    {
        var i = offset;
        while (i > 0 && text[i - 1] is not '\n' and not '\r')
        {
            i--;
        }

        return i;
    }

    private static int LineIndent(string text, int lineStart)
    {
        var n = 0;
        var i = lineStart;
        while (i < text.Length && text[i] is ' ' or '\t')
        {
            n += text[i] == '\t' ? 4 : 1;
            i++;
        }

        return n;
    }
}
