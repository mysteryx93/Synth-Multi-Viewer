using HanumanInstitute.ApiAviSynth;
using HanumanInstitute.ApiVapourSynth;

namespace HanumanInstitute.SynthMultiViewer.Services.Completion;

/// <summary>
/// Shares native catalogs across all editors for the current playback configuration.
/// </summary>
public static class EditorCatalogs
{
    /// <summary>
    /// Gets the API4 plugin catalog.
    /// </summary>
    public static CatalogCache VapourSynth
    {
        get;
    } = new(() => VsCatalog.Read().Select(x =>
        new FilterSymbol("core." + x.Namespace + "." + x.Name,
            x.Arguments?.Split(';', StringSplitOptions.RemoveEmptyEntries))).ToArray());
    /// <summary>
    /// Gets the AviSynth autoload catalog.
    /// </summary>
    public static CatalogCache AviSynth
    {
        get;
    } = new(() => AvsCatalog.Read().Select(x =>
        new FilterSymbol(x.Name, ParseAvsParameters(x.Arguments))).ToArray());

    /// <summary>
    /// Refreshes catalogs when library or plugin settings change.
    /// </summary>
    public static void Configure(Models.AppSettingsData settings)
    {
        VapourSynth.Refresh(string.Join("\0", settings.VapourSynthPath, settings.VapourSynthPluginFolders,
            settings.VapourSynthReplacePlugins));
        AviSynth.Refresh(string.Join("\0", settings.AviSynthPath, settings.AviSynthPluginFolders,
            settings.AviSynthReplacePlugins));
    }

    /// <summary>
    /// Explicitly refreshes both catalogs.
    /// </summary>
    public static void Refresh()
    {
        VapourSynth.Refresh("explicit", true);
        AviSynth.Refresh("explicit", true);
    }

    /// <summary>
    /// Decodes native parameter types and optional names; unsupported formats remain unknown.
    /// </summary>
    public static string[]? ParseAvsParameters(string? format)
    {
        if (format == null)
        {
            return null;
        }
        var result = new List<string>();
        for (var i = 0; i < format.Length; i++)
        {
            string? name = null;
            if (format[i] == '[')
            {
                var end = format.IndexOf(']', i + 1);
                if (end < 0)
                {
                    return null;
                }
                name = format[(i + 1)..end];
                i = end + 1;
                if (i >= format.Length)
                {
                    return null;
                }
            }
            var type = format[i] switch
            {
                'c' => "clip",
                'i' => "int",
                'f' => "float",
                'b' => "bool",
                's' => "string",
                '.' => "any",
                'n' => "function",
                'a' => "array",
                _ => null
            };
            if (type == null)
            {
                return null;
            }
            if (i + 1 < format.Length && format[i + 1] is '+' or '*')
            {
                type += format[++i];
            }
            result.Add(name == null ? type : type + " [" + name + "]");
        }
        return result.ToArray();
    }
}

