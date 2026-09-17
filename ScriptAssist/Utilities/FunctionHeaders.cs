namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Finds the closing parenthesis of a function header.
/// </summary>
internal static class FunctionHeaders
{
    /// <summary>
    /// Returns the index of the <c>)</c> matching <paramref name="open"/>, or -1.
    /// </summary>
    public static int MatchingClose(string text, int open)
    {
        var depth = 1;
        var quote = '\0';
        for (var i = open + 1; i < text.Length; i++)
        {
            var c = text[i];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Returns the index of the <c>}</c> matching <paramref name="open"/>, or -1.
    /// </summary>
    public static int MatchingBrace(string text, int open)
    {
        var depth = 1;
        for (var i = open + 1; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }
}