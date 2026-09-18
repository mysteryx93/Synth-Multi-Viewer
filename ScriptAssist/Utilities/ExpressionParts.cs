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
    public static string UnwrapParentheses(string expression) => UnwrapSpan(expression).Text;

    /// <summary>
    /// Strips matching outer parentheses and returns the inner text plus its offset in
    /// <paramref name="expression"/> so continuation context can keep the grouping.
    /// </summary>
    public static (string Text, int Offset) UnwrapSpan(string expression)
    {
        var start = 0;
        var end = expression.Length;
        Trim(expression, ref start, ref end);
        while (end - start >= 2 && IsParenthesized(expression, start, end))
        {
            start++;
            end--;
            Trim(expression, ref start, ref end);
        }

        return (expression[start..end], start);
    }

    private static void Trim(string expression, ref int start, ref int end)
    {
        while (start < end && char.IsWhiteSpace(expression[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(expression[end - 1]))
        {
            end--;
        }
    }

    private static bool IsParenthesized(string expression, int start, int end)
    {
        if (end - start < 2 || expression[start] != '(' || expression[end - 1] != ')')
        {
            return false;
        }

        var depth = 0;
        for (var i = start; i < end; i++)
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
                    return i == end - 1;
                }
            }
        }

        return false;
    }
}
