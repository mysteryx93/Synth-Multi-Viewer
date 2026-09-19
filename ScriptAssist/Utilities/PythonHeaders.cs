namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Shared recognition of Python <c>def</c> / <c>async def</c> headers.
/// </summary>
internal static class PythonHeaders
{
    /// <summary>
    /// Gets whether <paramref name="start"/> is a <c>def</c> or <c>async def</c> name prefix.
    /// </summary>
    public static bool StartsDef(string text, int start, int end)
    {
        var i = start;
        if (Keyword(text, i, end, "async"))
        {
            i = AfterKeyword(text, i, end, "async");
        }

        if (!Keyword(text, i, end, "def"))
        {
            return false;
        }

        i = AfterKeyword(text, i, end, "def");
        return i < end && BufferLexer.IsIdentifier(text[i]) && !char.IsDigit(text[i]);
    }

    /// <summary>
    /// Gets whether <paramref name="start"/> is a <c>def</c> or <c>async def</c> header.
    /// </summary>
    public static bool IsDef(string text, int start, int end) =>
        TryDef(text, start, end, out _, out _, out _);

    /// <summary>
    /// Reads a <c>def</c> or <c>async def</c> name and opening parenthesis.
    /// </summary>
    public static bool TryDef(string text, int start, int end, out string name, out int open, out bool async)
    {
        name = "";
        open = -1;
        async = false;
        var i = start;
        if (Keyword(text, i, end, "async"))
        {
            async = true;
            i = AfterKeyword(text, i, end, "async");
        }

        if (!Keyword(text, i, end, "def"))
        {
            return false;
        }

        i = AfterKeyword(text, i, end, "def");
        if (!TryIdent(text, ref i, end, out name))
        {
            return false;
        }

        SkipWs(text, ref i, end);
        if (i >= end || text[i] != '(')
        {
            return false;
        }

        open = i;
        return true;
    }

    private static bool Keyword(string text, int start, int end, string word)
    {
        if (end - start < word.Length)
        {
            return false;
        }

        if (!text.AsSpan(start, word.Length).Equals(word, StringComparison.Ordinal))
        {
            return false;
        }

        var after = start + word.Length;
        return after == end || !BufferLexer.IsIdentifier(text[after]);
    }

    private static int AfterKeyword(string text, int start, int end, string word)
    {
        var i = start + word.Length;
        SkipWs(text, ref i, end);
        return i;
    }

    private static void SkipWs(string text, ref int i, int end)
    {
        while (i < end && char.IsWhiteSpace(text[i]))
        {
            i++;
        }
    }

    private static bool TryIdent(string text, ref int i, int end, out string name)
    {
        name = "";
        if (i >= end || !BufferLexer.IsIdentifier(text[i]) || char.IsDigit(text[i]))
        {
            return false;
        }

        var start = i++;
        while (i < end && BufferLexer.IsIdentifier(text[i]))
        {
            i++;
        }

        name = text[start..i];
        return true;
    }
}
