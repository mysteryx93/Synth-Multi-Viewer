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

        return EscapeKeyword(Identifier(text));
    }

    /// <summary>
    /// Returns the type key from a Python or native VapourSynth parameter, without the name.
    /// </summary>
    public static string? PythonType(string parameter)
    {
        var text = StripDefault(parameter.Trim());
        var colon = IndexOfTopLevel(text, ':');
        if (colon < 0 || colon + 1 >= text.Length)
        {
            return null;
        }

        var type = text[(colon + 1)..].Trim();
        var extra = type.IndexOf(':');
        if (extra >= 0)
        {
            type = type[..extra];
        }

        type = type.Replace("[]", "", StringComparison.Ordinal).Trim();
        return type.Length == 0 ? null : type;
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
    /// Classifies a parameter in list order and updates whether later names are keyword-only.
    /// </summary>
    public static ParameterKind Classify(string parameter, ref bool keywordOnly)
    {
        var text = parameter.Trim();
        if (text == "/")
        {
            return ParameterKind.Separator;
        }

        if (text == "*")
        {
            keywordOnly = true;
            return ParameterKind.Separator;
        }

        if (text.StartsWith("**", StringComparison.Ordinal))
        {
            return ParameterKind.Kwargs;
        }

        if (text.StartsWith("*", StringComparison.Ordinal))
        {
            keywordOnly = true;
            return ParameterKind.Varargs;
        }

        return keywordOnly ? ParameterKind.KeywordOnly : ParameterKind.Positional;
    }

    /// <summary>
    /// Gets whether <paramref name="written"/> names <paramref name="parameterName"/>, including a
    /// native VapourSynth trailing <c>_</c> alias when the catalog name has none.
    /// </summary>
    public static bool ArgumentEquals(string parameterName, string written, StringComparison comparison)
    {
        if (parameterName.Equals(written, comparison))
        {
            return true;
        }

        return !parameterName.EndsWith('_') && written.Length == parameterName.Length + 1 &&
            written.EndsWith('_') &&
            parameterName.AsSpan().Equals(written.AsSpan(0, parameterName.Length), comparison);
    }

    /// <summary>
    /// Maps a positional argument index onto a physical parameter slot, absorbing <c>*args</c>.
    /// </summary>
    public static int MapPositional(string[] parameters, int positional)
    {
        var seen = 0;
        var varargs = -1;
        var keywordOnly = false;
        for (var i = 0; i < parameters.Length; i++)
        {
            var kind = Classify(parameters[i], ref keywordOnly);
            if (kind is ParameterKind.Separator or ParameterKind.Kwargs)
            {
                continue;
            }

            if (kind == ParameterKind.Varargs)
            {
                varargs = i;
                continue;
            }

            if (kind == ParameterKind.KeywordOnly && varargs >= 0)
            {
                continue;
            }

            if (seen == positional)
            {
                return i;
            }

            seen++;
        }

        return varargs >= 0 && positional >= seen ? varargs : parameters.Length;
    }

    /// <summary>
    /// Maps a keyword argument onto a physical parameter slot, falling back to <c>**kwargs</c>.
    /// </summary>
    public static int MapNamed(string[] parameters, string written, Func<string, string?> nameOf,
        StringComparison comparison)
    {
        var kwargs = -1;
        var keywordOnly = false;
        for (var i = 0; i < parameters.Length; i++)
        {
            var kind = Classify(parameters[i], ref keywordOnly);
            if (kind == ParameterKind.Kwargs)
            {
                kwargs = i;
                continue;
            }

            var name = nameOf(parameters[i]);
            if (name != null && ArgumentEquals(name, written, comparison))
            {
                return i;
            }
        }

        return kwargs >= 0 ? kwargs : parameters.Length;
    }

    /// <summary>
    /// Splits a trailing <c>as</c> alias, accepting any whitespace around the keyword.
    /// </summary>
    public static bool TryAlias(string text, out string source, out string alias)
    {
        var asAt = LastAsKeyword(text);
        if (asAt < 0)
        {
            source = text.Trim();
            alias = source;
            return false;
        }

        source = text[..asAt].Trim();
        alias = text[(asAt + 2)..].Trim();
        return source.Length > 0 && alias.Length > 0;
    }

    private static int LastAsKeyword(string text)
    {
        var last = -1;
        for (var i = 0; i + 2 <= text.Length; i++)
        {
            if (text[i] != 'a' || text[i + 1] != 's')
            {
                continue;
            }

            if (i > 0 && !char.IsWhiteSpace(text[i - 1]))
            {
                continue;
            }

            if (i + 2 < text.Length && !char.IsWhiteSpace(text[i + 2]))
            {
                continue;
            }

            last = i;
        }

        return last;
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
    /// Returns the AviSynth type word from <c>int [height]</c> / <c>clip c</c>, without the name.
    /// </summary>
    public static string? AviSynthType(string parameter)
    {
        var text = parameter.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        var i = 0;
        while (i < text.Length && (char.IsLetter(text[i]) || text[i] == '_'))
        {
            i++;
        }

        if (i == 0)
        {
            return null;
        }

        var word = text[..i];
        return IsAviSynthTypeWord(word) ? word : null;
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
    /// Index of a keyword <c>=</c> that is not part of <c>==</c>, <c>!=</c>, <c>&lt;=</c>, or <c>&gt;=</c>.
    /// </summary>
    public static int KeywordEqualsIndex(string text) => KeywordEqualsIndex(text, 0, text.Length, topLevel: false);

    /// <summary>
    /// Index of a top-level keyword <c>=</c> in <paramref name="text"/>.
    /// </summary>
    public static int TopLevelKeywordEquals(string text) => KeywordEqualsIndex(text, 0, text.Length, topLevel: true);

    private static int KeywordEqualsIndex(string text, int start, int end, bool topLevel)
    {
        var depth = 0;
        for (var i = start; i < end; i++)
        {
            var c = text[i];
            if (topLevel)
            {
                if (c is '(' or '[' or '{')
                {
                    depth++;
                    continue;
                }

                if (c is ')' or ']' or '}' && depth > 0)
                {
                    depth--;
                    continue;
                }

                if (depth > 0)
                {
                    continue;
                }
            }

            if (c != '=')
            {
                continue;
            }

            if (i + 1 < end && text[i + 1] == '=')
            {
                i++;
                continue;
            }

            if (i > start && text[i - 1] is '=' or '!' or '<' or '>')
            {
                continue;
            }

            return i;
        }

        return -1;
    }

    /// <summary>
    /// Index of a keyword <c>=</c> leftover marker for call-scanner reuse.
    /// </summary>
    internal static bool IsKeywordAssign(string text, int index)
    {
        if (index < 0 || index >= text.Length || text[index] != '=')
        {
            return false;
        }

        if (index + 1 < text.Length && text[index + 1] == '=')
        {
            return false;
        }

        return index == 0 || text[index - 1] is not ('=' or '!' or '<' or '>');
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
        text.Equals("function", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("any", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("array", StringComparison.OrdinalIgnoreCase);

    private static readonly HashSet<string> PythonKeywords = new(StringComparer.Ordinal)
    {
        "False", "None", "True", "and", "as", "assert", "async", "await", "break", "class", "continue",
        "def", "del", "elif", "else", "except", "finally", "for", "from", "global", "if", "import", "in",
        "is", "lambda", "nonlocal", "not", "or", "pass", "raise", "return", "try", "while", "with", "yield"
    };

    private static string? EscapeKeyword(string? name) =>
        name != null && PythonKeywords.Contains(name) ? name + "_" : name;

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

/// <summary>
/// How a parameter participates in positional vs keyword binding.
/// </summary>
internal enum ParameterKind
{
    Positional,
    Varargs,
    KeywordOnly,
    Separator,
    Kwargs
}
