namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Reads argument names from catalog parameter strings.
/// </summary>
internal static class ParameterNames
{
    /// <summary>
    /// Returns the argument name for VapourSynth <c>left:int:opt</c>, Python <c>radius=1</c>,
    /// or AviSynth <c>int [left]</c> / <c>int "left"</c> / <c>clip c</c> forms.
    /// </summary>
    public static string? Of(string parameter)
    {
        var text = parameter.Trim();
        if (text.Length == 0 || text[0] == '*')
        {
            return null;
        }

        var bracket = text.IndexOf('[');
        if (bracket >= 0)
        {
            var end = text.IndexOf(']', bracket + 1);
            if (end > bracket + 1)
            {
                return text[(bracket + 1)..end];
            }
        }

        var eq = text.IndexOf('=');
        if (eq >= 0)
        {
            text = text[..eq].Trim();
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

        var colon = text.IndexOf(':');
        if (colon > 0)
        {
            var name = text[..colon].Trim();
            return name.Length == 0 ? null : name;
        }

        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return parts[^1];
        }

        return parts.Length == 1 && BufferLexer.IsIdentifier(parts[0][0]) ? parts[0] : null;
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

    private static void Add(List<string> items, string piece)
    {
        var value = piece.Trim();
        if (value.Length > 0)
        {
            items.Add(value);
        }
    }
}
