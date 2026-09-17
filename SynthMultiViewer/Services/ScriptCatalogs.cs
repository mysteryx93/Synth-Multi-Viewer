using HanumanInstitute.ApiAviSynth;
using HanumanInstitute.ApiVapourSynth;
using HanumanInstitute.ScriptAssist.AviSynth;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Maps native plugin metadata into ScriptAssist symbols.
/// </summary>
internal static class ScriptCatalogs
{
    /// <summary>
    /// Copies the API4 plugin catalog.
    /// </summary>
    public static IReadOnlyList<Symbol> VapourSynth() =>
        VsCatalog.Read().Select(x => new Symbol(
            "core." + x.Namespace + "." + x.Name,
            x.Arguments?.Split(';', StringSplitOptions.RemoveEmptyEntries),
            ReturnType: x.ReturnType)).ToArray();

    /// <summary>
    /// Copies the AviSynth autoload catalog and named headers from plugin-folder scripts.
    /// </summary>
    public static IReadOnlyList<Symbol> AviSynth()
    {
        var native = AvsCatalog.Read()
            .Select(x => new Symbol(x.Name, AviSynthParameters.Parse(x.Arguments)))
            .ToArray();
        var lexer = new AviSynthLanguage().Lexer;
        var parsed = new List<Symbol>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in PluginScripts(AvsScript.GetPluginDirectories(), [".avsi", ".avs"]))
        {
            try
            {
                var path = Path.GetFullPath(file);
                if (!visited.Add(path))
                {
                    continue;
                }

                var text = File.ReadAllText(path);
                parsed.AddRange(AviSynthFunctions.Parse(text, lexer));
                AviSynthFunctions.AddImports(text, path, ScriptIncludeIO.AviSynth, parsed, visited, lexer,
                    CancellationToken.None);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return AviSynthFunctions.UnionByName(native, parsed);
    }

    private static IEnumerable<string> PluginScripts(IReadOnlyList<string> directories, string[] extensions)
    {
        foreach (var directory in directories)
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                if (extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                {
                    yield return file;
                }
            }
        }
    }
}
