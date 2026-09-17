namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Reads argument names from catalog parameter strings.
/// </summary>
internal static class ParameterNames
{
    /// <summary>
    /// Returns the argument name for Python <c>radius: Optional[int] = None</c> or native
    /// VapourSynth <c>left:int:opt</c> forms.
    /// </summary>
    public static string? OfPython(string parameter)
    {
        var text = StripDefault(parameter.Trim());
        if (text.Length == 0 || IsSeparator(text) || text[0] == '*')
        {
            return null;
        }

        var colon = IndexOfTopLevel(text, ':');
        if (colon > 0)
        {
            text = text[..colon].Trim();
        }

        return Identifier(text);
    }

    /// <summary>
    /// Gets whether <paramref name="parameter"/> is a Python <c>*</c> or <c>/</c> separator.
    /// </summary>
    public static bool IsSeparator(string parameter)
    {
        var text = parameter.Trim();
        return text is "*" or "/";
    }

    /// <summary>
    /// Returns the argument name for AviSynth <c>int [left]</c> / <c>int "left"</c> / <c>clip c</c>.
    /// </summary>
    public static string? OfAviSynth(string parameter)
    {
        var text = parameter.Trim();
        if (text.Length == 0 || text[0] == '*')
        {
            return null;
        }

        var bracket = IndexOfAviSynthOptional(text);
        if (bracket >= 0)
        {
            var end = text.IndexOf(']', bracket + 1);
            if (end > bracket + 1)
            {
                return Identifier(text[(bracket + 1)..end].Trim()) ?? text[(bracket + 1)..end].Trim();
            }
        }

        var quote = text.IndexOf('"');
        if (quote >= 0)
        {
            var end = text.IndexOf('"', quote + 1);
            if (end > quote + 1)
            {
                return text[(quote + 1)..end];
            }
        }

        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return Identifier(parts[^1]);
        }

        if (parts.Length == 1 && !IsAviSynthTypeWord(parts[0]))
        {
            return Identifier(parts[0]);
        }

        return null;
    }

    /// <summary>
    /// Splits a parameter list on commas that are not inside <c>()</c>, <c>[]</c>, or quotes.
    /// </summary>
    public static string[] Split(string inside)
    {
        var items = new List<string>();
        var start = 0;
        var depth = 0;
        var quote = '\0';
        for (var i = 0; i < inside.Length; i++)
        {
            var c = inside[i];
            if (quote != '\0')
            {
                if (c == '\\' && i + 1 < inside.Length)
                {
                    i++;
                    continue;
                }

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
            else if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}' && depth > 0)
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                Add(items, inside[start..i]);
                start = i + 1;
            }
        }

        Add(items, inside[start..]);
        return items.ToArray();
    }

    /// <summary>
    /// Gets whether the caret is at the start of an argument (empty or a lone identifier).
    /// </summary>
    public static bool AtArgumentStart(string current)
    {
        if (KeywordEqualsIndex(current) >= 0)
        {
            return false;
        }

        var text = current.Trim();
        return text.Length == 0 || Identifier(text) != null;
    }

    /// <summary>
    /// Counts previous positional arguments in an unclosed argument list.
    /// </summary>
    public static int PositionalConsumed(string argumentList)
    {
        var parts = Split(argumentList);
        var trailingComma = argumentList.TrimEnd().EndsWith(',');
        var count = trailingComma ? parts.Length : Math.Max(0, parts.Length - 1);
        var consumed = 0;
        for (var i = 0; i < count; i++)
        {
            if (KeywordEqualsIndex(parts[i]) < 0)
            {
                consumed++;
            }
        }

        return consumed;
    }

    /// <summary>
    /// Index of a keyword <c>=</c> that is not part of <c>==</c>, <c>!=</c>, <c>&lt;=</c>, or <c>&gt;=</c>.
    /// </summary>
    public static int KeywordEqualsIndex(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '=')
            {
                continue;
            }

            if (i + 1 < text.Length && text[i + 1] == '=')
            {
                i++;
                continue;
            }

            if (i > 0 && text[i - 1] is '=' or '!' or '<' or '>')
            {
                continue;
            }

            return i;
        }

        return -1;
    }

    private static int IndexOfAviSynthOptional(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '[')
            {
                continue;
            }

            if (i == 0 || char.IsWhiteSpace(text[i - 1]))
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfTopLevel(string text, char delimiter)
    {
        var depth = 0;
        var quote = '\0';
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quote != '\0')
            {
                if (c == '\\' && i + 1 < text.Length)
                {
                    i++;
                    continue;
                }

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
            else if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}' && depth > 0)
            {
                depth--;
            }
            else if (c == delimiter && depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static string StripDefault(string text)
    {
        var eq = KeywordEqualsIndex(text);
        return eq >= 0 ? text[..eq].Trim() : text;
    }

    private static bool IsAviSynthTypeWord(string text) =>
        text.Equals("clip", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("int", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("float", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("bool", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("string", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("val", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("func", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("array", StringComparison.OrdinalIgnoreCase);

    private static string? Identifier(string text)
    {
        if (text.Length == 0 || !BufferLexer.IsIdentifier(text[0]) || char.IsDigit(text[0]))
        {
            return null;
        }

        var i = 1;
        while (i < text.Length && BufferLexer.IsIdentifier(text[i]))
        {
            i++;
        }

        return i == text.Length ? text : null;
    }

    private static void Add(List<string> items, string piece)
    {
        var value = piece.Trim();
        if (value.Length > 0)
        {
            items.Add(value);
        }
    }
}
