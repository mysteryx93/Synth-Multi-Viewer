namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Maps a native VapourSynth dump into catalog symbols.
/// </summary>
public sealed class VapourSynthSymbolSource(IVapourSynthNativeCatalog native) : ISymbolSource
{
    private readonly IVapourSynthNativeCatalog _native = native.CheckNotNull();

    /// <inheritdoc />
    public IReadOnlyList<Symbol> Enumerate()
    {
        var functions = _native.Read();
        var symbols = new List<Symbol>(functions.Count);
        var titles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var function in functions)
        {
            if (function.PluginName.HasValue() && titles.Add(function.Namespace))
            {
                symbols.Add(new("core." + function.Namespace, null, SymbolKind.Namespace,
                    Title: function.PluginName));
            }

            symbols.Add(new(
                "core." + function.Namespace + "." + function.Name,
                function.Arguments?.Split(';', StringSplitOptions.RemoveEmptyEntries),
                ReturnType: function.ReturnType,
                Title: function.PluginName));
        }

        return symbols;
    }
}
