namespace HanumanInstitute.ScriptAssist.AviSynth;

/// <summary>
/// Maps a native AviSynth dump and autoload scripts into catalog symbols.
/// </summary>
public sealed class AviSynthSymbolSource : ISymbolSource
{
    private static readonly string[] ScriptExtensions = [".avsi", ".avs"];
    private readonly IAviSynthNativeCatalog _native;
    private readonly IScriptDirectory _autoload;
    private readonly IIncludeSource? _includes;

    /// <summary>
    /// Creates a source that maps <paramref name="native"/> filters and merges plugin-folder scripts.
    /// </summary>
    public AviSynthSymbolSource(
        IAviSynthNativeCatalog native,
        IScriptDirectory autoload,
        IIncludeSource? includes = null)
    {
        _native = native.CheckNotNull();
        _autoload = autoload.CheckNotNull();
        _includes = includes;
    }

    /// <inheritdoc />
    public IReadOnlyList<Symbol> Enumerate()
    {
        var mapped = _native.Read().Select(Map).ToArray();
        var lexer = new AviSynthLanguage().Lexer;
        var parsed = new List<Symbol>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in PluginScripts())
        {
            if (!visited.Add(file))
            {
                continue;
            }

            var text = _autoload.TryRead(file);
            if (text == null)
            {
                continue;
            }

            parsed.AddRange(AviSynthFunctions.Parse(text, lexer));
            AviSynthFunctions.AddImports(text, file, _includes, parsed, visited, lexer, CancellationToken.None);
        }

        return AviSynthFunctions.UnionByName(mapped, parsed);
    }

    private IEnumerable<string> PluginScripts()
    {
        foreach (var directory in _autoload.Roots())
        {
            foreach (var file in _autoload.Files(directory, ScriptExtensions))
            {
                yield return file;
            }
        }
    }

    private static Symbol Map(AviSynthFilter filter) =>
        new(filter.Name, AviSynthParameters.Parse(filter.Arguments), Group: GroupOf(filter.Category));

    private static string GroupOf(string category) =>
        category switch
        {
            "InternalFunctions" => "Internal",
            "PluginFunctions" => "Plugin",
            "UserFunctions" => "User",
            _ => "Plugin"
        };
}
