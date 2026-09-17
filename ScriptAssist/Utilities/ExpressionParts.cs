namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Splits an expression on top-level <c>+</c> and <c>*</c> outside brackets.
/// </summary>
internal static class ExpressionParts
{
    /// <summary>
    /// Returns trimmed operands of a clip-copy expression.
    /// </summary>
    public static IReadOnlyList<string> SplitAddMul(string expression)
    {
        var parts = new List<string>();
        var start = 0;
        var depth = 0;
        var quote = '\0';
        for (var i = 0; i < expression.Length; i++)
        {
            var c = expression[i];
            if (quote != '\0')
            {
                if (c == '\\' && i + 1 < expression.Length)
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
            else if (c is ')' or ']' or '}')
            {
                if (depth > 0)
                {
                    depth--;
                }
            }
            else if (depth == 0 && c is '+' or '*')
            {
                parts.Add(expression[start..i].Trim());
                start = i + 1;
            }
        }

        parts.Add(expression[start..].Trim());
        return parts;
    }

    /// <summary>
    /// Returns the first <paramref name="symbol"/> not inside <c>()</c>, <c>[]</c>, or <c>{}</c>, or -1.
    /// </summary>
    public static int IndexOutsideBrackets(string expression, char symbol)
    {
        var depth = 0;
        for (var i = 0; i < expression.Length; i++)
        {
            var c = expression[i];
            if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}' && depth > 0)
            {
                depth--;
            }
            else if (depth == 0 && c == symbol)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Strips matching outer parentheses so <c>(cond ? a : b)</c> is a ternary.
    /// </summary>
    public static string UnwrapParentheses(string expression)
    {
        var trimmed = expression.Trim();
        while (IsParenthesized(trimmed))
        {
            trimmed = trimmed[1..^1].Trim();
        }

        return trimmed;
    }

    private static bool IsParenthesized(string expression)
    {
        if (expression.Length < 2 || expression[0] != '(' || expression[^1] != ')')
        {
            return false;
        }

        var depth = 0;
        for (var i = 0; i < expression.Length; i++)
        {
            var c = expression[i];
            if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}' && depth > 0)
            {
                depth--;
                if (depth == 0)
                {
                    return i == expression.Length - 1;
                }
            }
        }

        return false;
    }
}
