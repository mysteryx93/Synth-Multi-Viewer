using System.Text.RegularExpressions;

namespace HanumanInstitute.ScriptAssist.AviSynth;

/// <summary>
/// Parses AviSynth <c>function Name(...)</c> headers from script text.
/// </summary>
public static class AviSynthFunctions
{
    /// <summary>
    /// Returns declared functions; comments are ignored.
    /// </summary>
    public static IReadOnlyList<Symbol> Parse(string text, LexerOptions lexer, CancellationToken token = default)
    {
        var spans = new List<Symbol>();
        foreach (var span in Spans(text, lexer, token))
        {
            spans.Add(span.Symbol);
        }

        return spans;
    }

    /// <summary>
    /// Returns declared functions with header offsets; comments and line continuations are ignored.
    /// </summary>
    internal static IReadOnlyList<AviSynthFunctionSpan> Spans(string text, LexerOptions lexer,
        CancellationToken token = default)
    {
        var clean = AviSynthPatterns.Clean(text, lexer, token: token);
        var quoted = AviSynthPatterns.Clean(text, lexer, maskStrings: false, token: token);
        var buffer = new List<AviSynthFunctionSpan>();
        foreach (Match match in AviSynthPatterns.Functions().Matches(clean))
        {
            token.ThrowIfCancellationRequested();
            var open = match.Index + match.Length - 1;
            var close = FunctionHeaders.MatchingClose(clean, open);
            var end = close < 0 ? clean.Length : close;
            var inside = AviSynthPatterns.Whitespace().Replace(quoted[(open + 1)..end], " ");
            buffer.Add(new AviSynthFunctionSpan(new Symbol(match.Groups[1].Value, ParameterNames.Split(inside)),
                match.Index, close < 0 ? Math.Max(open, clean.Length - 1) : close));
        }

        return buffer;
    }

    /// <summary>
    /// Follows <c>Import</c> specifiers with <paramref name="read"/> and returns parsed functions.
    /// </summary>
    public static IReadOnlyList<Symbol> LoadImports(string text, string? documentPath, IncludeReader? read,
        LexerOptions lexer, CancellationToken token = default)
    {
        var buffer = new List<Symbol>();
        AddImports(text, documentPath, read, buffer, new HashSet<string>(StringComparer.Ordinal), lexer, token);
        return buffer;
    }

    /// <summary>
    /// Follows <c>Import</c> specifiers with <paramref name="read"/> and appends parsed functions.
    /// </summary>
    public static void AddImports(string text, string? documentPath, IncludeReader? read, List<Symbol> buffer,
        HashSet<string> visited, LexerOptions lexer, CancellationToken token)
    {
        if (read == null)
        {
            return;
        }

        var quoted = AviSynthPatterns.Clean(text, lexer, maskStrings: false, token: token);
        var clean = AviSynthPatterns.Clean(text, lexer, token: token);
        foreach (Match match in AviSynthPatterns.Import().Matches(quoted))
        {
            token.ThrowIfCancellationRequested();
            if (match.Index >= clean.Length || !char.IsLetter(clean[match.Index]))
            {
                continue;
            }

            var specifier = match.Groups[1].Success && match.Groups[1].Length > 0
                ? match.Groups[1].Value
                : match.Groups[2].Value.Replace("\"\"", "\"", StringComparison.Ordinal);
            var file = read(specifier, documentPath);
            if (file == null || !visited.Add(file.Value.Path))
            {
                continue;
            }

            buffer.AddRange(Parse(file.Value.Text, lexer, token));
            AddImports(file.Value.Text, file.Value.Path, read, buffer, visited, lexer, token);
        }
    }

    /// <summary>
    /// Merges parsed user-function headers into a native catalog; compiled plugins keep native signatures.
    /// </summary>
    public static IReadOnlyList<Symbol> UnionByName(IReadOnlyList<Symbol> native, IReadOnlyList<Symbol> parsed)
    {
        var result = new List<Symbol>(native.Count + parsed.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in native.GroupBy(symbol => symbol.Name, StringComparer.OrdinalIgnoreCase))
        {
            seen.Add(group.Key);
            var parsedGroup = parsed.Where(symbol => symbol.Name.Equals(group.Key, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var nativeGroup = group.ToList();
            result.AddRange(EnrichGroup(nativeGroup, parsedGroup));
        }

        foreach (var group in parsed.GroupBy(symbol => symbol.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (seen.Add(group.Key))
            {
                result.AddRange(group);
            }
        }

        return result;
    }

    private static IReadOnlyList<Symbol> EnrichGroup(List<Symbol> nativeGroup, List<Symbol> parsedGroup)
    {
        if (parsedGroup.Count == 0)
        {
            return nativeGroup;
        }

        if (nativeGroup.Count == 1 && parsedGroup.Count == 1 && NamedCount(nativeGroup[0]) == 0 &&
            NamedCount(parsedGroup[0]) > 0 && IsIncompletePrefix(nativeGroup[0], parsedGroup[0]))
        {
            return parsedGroup;
        }

        var used = new HashSet<int>();
        var enriched = new List<Symbol>(nativeGroup.Count);
        foreach (var nativeSymbol in nativeGroup)
        {
            var match = -1;
            if (NamedCount(nativeSymbol) == 0)
            {
                for (var i = 0; i < parsedGroup.Count; i++)
                {
                    if (used.Contains(i) || NamedCount(parsedGroup[i]) == 0 ||
                        !SameShape(nativeSymbol, parsedGroup[i]))
                    {
                        continue;
                    }

                    match = i;
                    break;
                }
            }

            if (match >= 0)
            {
                used.Add(match);
                enriched.Add(parsedGroup[match]);
            }
            else
            {
                enriched.Add(nativeSymbol);
            }
        }

        return enriched;
    }

    private static bool IsIncompletePrefix(Symbol native, Symbol parsed)
    {
        var a = native.Parameters ?? [];
        var b = parsed.Parameters ?? [];
        if (a.Length == 0 || a.Length >= b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            if (!TypeKey(a[i]).Equals(TypeKey(b[i]), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameShape(Symbol left, Symbol right)
    {
        var a = left.Parameters ?? [];
        var b = right.Parameters ?? [];
        if (a.Length != b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            if (!TypeKey(a[i]).Equals(TypeKey(b[i]), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string TypeKey(string parameter)
    {
        var text = parameter.Trim();
        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "";
        }

        return parts[0];
    }

    private static int NamedCount(Symbol symbol)
    {
        if (symbol.Parameters == null)
        {
            return 0;
        }

        var count = 0;
        foreach (var parameter in symbol.Parameters)
        {
            var name = ParameterNames.OfAviSynth(parameter);
            if (name != null && !parameter.Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }
}

/// <summary>
/// A parsed <c>function</c> header and the offset of its closing <c>)</c>.
/// </summary>
internal readonly record struct AviSynthFunctionSpan(Symbol Symbol, int Start, int ParenClose);