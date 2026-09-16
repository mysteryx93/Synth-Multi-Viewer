using System.Text.RegularExpressions;

namespace HanumanInstitute.SynthMultiViewer.Services.Completion;

/// <summary>
/// Combines cached native filters with a small buffer lexer and language-specific context rules.
/// </summary>
public sealed partial class EditorLanguageService(bool aviSynth, CatalogCache catalog) : IEditorLanguageService
{
    [GeneratedRegex(@"\bfunction\s+([\p{L}_][\p{L}\p{N}_]*)\s*\(([^)]*)\)", RegexOptions.IgnoreCase)]
    private static partial Regex AviSynthFunctions();

    [GeneratedRegex(@"\bimport\s+vapoursynth\s+as\s+([\p{L}_][\p{L}\p{N}_]*)")]
    private static partial Regex ImportVapourSynth();

    [GeneratedRegex(@"\bfrom\s+vapoursynth\s+import\s+core(?:\s+as\s+([\p{L}_][\p{L}\p{N}_]*))?")]
    private static partial Regex FromVapourSynthCore();

    [GeneratedRegex(@"^\s*(?:global\s+)?([\p{L}_][\p{L}\p{N}_\p{M}]*)\s*=(?!=)\s*(.*)$", RegexOptions.Multiline)]
    private static partial Regex NameAssign();

    [GeneratedRegex(@"^\s*([\p{L}_][\p{L}\p{N}_\p{M}]*(?:\s*,\s*[\p{L}_][\p{L}\p{N}_\p{M}]*)+)\s*=", RegexOptions.Multiline)]
    private static partial Regex UnpackAssign();

    [GeneratedRegex(@"\bimport\s+([\p{L}_][\p{L}\p{N}_.]*)(?:\s+as\s+([\p{L}_][\p{L}\p{N}_]*))?")]
    private static partial Regex ImportAs();

    [GeneratedRegex(@"\bfrom\s+\S+\s+import\s+([^\n#]+)")]
    private static partial Regex FromImport();

    [GeneratedRegex(@"^([\p{L}_][\p{L}\p{N}_]*)\.(core|get_core)\s*(?:\([^)]*\))?\s*(?:#.*)?$")]
    private static partial Regex CoreRhs();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <inheritdoc />
    public async Task<EditorReply> GetAsync(string text, int caret, CancellationToken cancellationToken)
    {
        var native = await catalog.GetAsync(cancellationToken).ConfigureAwait(false);
        return await Task.Run(() => Analyze(text, caret, native, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Computes completion replacements and nested call insight from an immutable snapshot.
    /// </summary>
    public EditorReply Analyze(string text, int caret, IReadOnlyList<FilterSymbol> native, CancellationToken token = default)
    {
        if (caret < 0 || caret > text.Length)
        {
            return new([], null);
        }

        var prefix = BufferLexer.Mask(text[..caret], aviSynth);
        if (prefix.InLiteral)
        {
            return new([], null);
        }

        token.ThrowIfCancellationRequested();
        var symbols = CollectSymbols(text, native);
        var aliases = aviSynth ? VsAliases.None : CollectVsAliases(BufferLexer.Mask(text, false).Code);
        var (start, end, path) = ReadPath(text, caret);
        var normalized = NormalizeVsPath(path, aliases);
        var comparison = aviSynth ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var typed = text[start..caret];
        var items = new List<EditorCompletion>();
        foreach (var symbol in symbols)
        {
            if (!TryMemberName(symbol, normalized, comparison, out var name))
            {
                continue;
            }

            if (name.StartsWith(typed, comparison))
            {
                items.Add(new(name, start, end - start, symbol.Kind, symbol.Signature));
            }
        }

        return new(items.DistinctBy(x => x.InsertionText, aviSynth ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .OrderBy(x => x.InsertionText).ToArray(), FindCall(prefix.Code, symbols, comparison, aliases));
    }

    private List<FilterSymbol> CollectSymbols(string text, IReadOnlyList<FilterSymbol> native)
    {
        var clean = BufferLexer.Mask(text, aviSynth).Code;
        var symbols = new List<FilterSymbol>(native);
        var keywords = aviSynth
            ? "function return global true false last try catch if else"
            : "import from as def return if else elif for while in True False None and or not vs core";
        symbols.AddRange(keywords.Split(' ').Select(x => new FilterSymbol(x, null, CompletionKind.Keyword)));
        foreach (var name in CollectLocals(clean))
        {
            symbols.Add(new(name, null, CompletionKind.Local));
        }

        if (aviSynth)
        {
            var declarations = BufferLexer.Mask(text, true, false).Code;
            foreach (Match match in AviSynthFunctions().Matches(clean))
            {
                var group = match.Groups[2];
                var parameters = declarations.Substring(group.Index, group.Length)
                    .Replace("\\", "", StringComparison.Ordinal)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => Whitespace().Replace(x.Trim(), " "))
                    .ToArray();
                symbols.Add(new(match.Groups[1].Value, parameters));
            }
        }
        else
        {
            symbols.Add(new("vs.core", null, CompletionKind.Namespace));
            symbols.AddRange(native
                .Select(NamespaceOf)
                .Where(x => x.Length > 0)
                .Distinct()
                .Select(x => new FilterSymbol(x, null, CompletionKind.Namespace)));
        }

        return symbols;
    }

    private bool TryMemberName(FilterSymbol symbol, string path, StringComparison comparison, out string name)
    {
        if (aviSynth)
        {
            name = symbol.Name;
            return path.Length == 0 || symbol.Kind == CompletionKind.Function;
        }

        if (!symbol.Name.StartsWith(path, comparison))
        {
            name = "";
            return false;
        }

        name = symbol.Name[path.Length..];
        return !name.Contains('.');
    }

    private static (int Start, int End, string Path) ReadPath(string text, int caret)
    {
        var start = caret;
        while (start > 0 && BufferLexer.IsIdentifier(text[start - 1]))
        {
            start--;
        }

        var end = caret;
        while (end < text.Length && BufferLexer.IsIdentifier(text[end]))
        {
            end++;
        }

        var pathStart = start;
        while (pathStart > 0 && (BufferLexer.IsIdentifier(text[pathStart - 1]) || text[pathStart - 1] == '.'))
        {
            pathStart--;
        }

        return (start, end, text[pathStart..start]);
    }

    private static string NamespaceOf(FilterSymbol symbol)
    {
        var index = symbol.Name.LastIndexOf('.');
        return index <= 0 ? symbol.Name : symbol.Name[..index];
    }

    private sealed record CallFrame(char Delimiter, int Offset, int Parameter);

    private CallInsight? FindCall(string code, List<FilterSymbol> symbols, StringComparison comparison, VsAliases aliases)
    {
        var stack = new Stack<CallFrame>();
        for (var i = 0; i < code.Length; i++)
        {
            var c = code[i];
            if (c is '(' or '[' or '{')
            {
                stack.Push(new(c, i, 0));
            }
            else if (c is ')' or ']' or '}')
            {
                if (stack.Count > 0)
                {
                    stack.Pop();
                }
            }
            else if (c == ',' && stack.Count > 0)
            {
                var frame = stack.Pop();
                stack.Push(frame with { Parameter = frame.Parameter + 1 });
            }
        }

        foreach (var frame in stack)
        {
            if (frame.Delimiter != '(')
            {
                continue;
            }

            var end = frame.Offset;
            while (end > 0 && char.IsWhiteSpace(code[end - 1]))
            {
                end--;
            }

            var start = end;
            while (start > 0 && (BufferLexer.IsIdentifier(code[start - 1]) || code[start - 1] == '.'))
            {
                start--;
            }

            var name = code[start..end];
            var implicitClip = aviSynth && name.Contains('.');
            if (implicitClip)
            {
                name = name[(name.LastIndexOf('.') + 1)..];
            }

            name = NormalizeVsPath(name, aliases);

            var matches = symbols.Where(x => x.Kind == CompletionKind.Function && x.Name.Equals(name, comparison)).ToArray();
            if (matches.Length > 0)
            {
                if (aviSynth && !implicitClip)
                {
                    matches = [.. matches, .. matches
                        .Where(x => x.Parameters?.FirstOrDefault()?.StartsWith("clip", StringComparison.OrdinalIgnoreCase) == true)
                        .Select(x => x with { Parameters = x.Parameters![1..], ImplicitLast = true })];
                }

                return new(matches, frame.Parameter, implicitClip);
            }

            if (name.Length > 0)
            {
                return null;
            }
        }

        return null;
    }

    private readonly record struct VsAliases(HashSet<string> Modules, HashSet<string> Cores)
    {
        public static VsAliases None { get; } = new(new(StringComparer.Ordinal), new(StringComparer.Ordinal));
    }

    private static IEnumerable<string> CollectLocals(string clean)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in NameAssign().Matches(clean))
        {
            names.Add(match.Groups[1].Value);
        }

        foreach (Match match in UnpackAssign().Matches(clean))
        {
            foreach (var name in match.Groups[1].Value.Split(','))
            {
                names.Add(name.Trim());
            }
        }

        foreach (Match match in ImportAs().Matches(clean))
        {
            names.Add(match.Groups[2].Success && match.Groups[2].Length > 0
                ? match.Groups[2].Value
                : match.Groups[1].Value[(match.Groups[1].Value.LastIndexOf('.') + 1)..]);
        }

        foreach (Match match in FromImport().Matches(clean))
        {
            foreach (var part in match.Groups[1].Value.Split(','))
            {
                var piece = part.Trim();
                if (piece.Length == 0 || piece == "*")
                {
                    continue;
                }

                var alias = piece.LastIndexOf(" as ", StringComparison.Ordinal);
                names.Add((alias < 0 ? piece : piece[(alias + 4)..]).Trim());
            }
        }

        names.Remove("");
        return names;
    }

    private static VsAliases CollectVsAliases(string clean)
    {
        var modules = new HashSet<string>(StringComparer.Ordinal) { "vs", "vapoursynth" };
        foreach (Match match in ImportVapourSynth().Matches(clean))
        {
            modules.Add(match.Groups[1].Value);
        }

        var cores = new HashSet<string>(StringComparer.Ordinal) { "core" };
        foreach (Match match in FromVapourSynthCore().Matches(clean))
        {
            cores.Add(match.Groups[1].Success && match.Groups[1].Length > 0 ? match.Groups[1].Value : "core");
        }

        foreach (Match match in NameAssign().Matches(clean))
        {
            var name = match.Groups[1].Value;
            if (IsCoreRhs(match.Groups[2].Value.Trim(), modules, cores))
            {
                cores.Add(name);
            }
            else if (name != "core")
            {
                cores.Remove(name);
            }
        }

        return new(modules, cores);
    }

    private static bool IsCoreRhs(string rhs, HashSet<string> modules, HashSet<string> cores)
    {
        if (cores.Contains(rhs))
        {
            return true;
        }

        var match = CoreRhs().Match(rhs);
        return match.Success && modules.Contains(match.Groups[1].Value);
    }

    private static string NormalizeVsPath(string path, VsAliases aliases)
    {
        foreach (var module in aliases.Modules)
        {
            if (path.StartsWith(module + ".core", StringComparison.Ordinal))
            {
                path = "vs" + path[module.Length..];
                break;
            }

            if (path == module + ".")
            {
                path = "vs.";
                break;
            }
        }

        foreach (var alias in aliases.Cores)
        {
            if (alias != "core" && path.StartsWith(alias + ".", StringComparison.Ordinal))
            {
                path = "core." + path[(alias.Length + 1)..];
                break;
            }
        }

        return path.StartsWith("vs.core.", StringComparison.Ordinal) ? path[3..] : path;
    }
}
