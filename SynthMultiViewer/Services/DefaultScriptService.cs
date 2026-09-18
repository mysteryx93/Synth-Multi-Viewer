using Avalonia.Platform;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Loads default scripts from application assets.
/// </summary>
public sealed class DefaultScriptService : IDefaultScriptService
{
    /// <inheritdoc />
    public string VapourSynth => field ??= Read("DefaultVapourSynth.vpy");

    /// <inheritdoc />
    public string AviSynth => field ??= Read("DefaultAvisynth.avs");

    private static string Read(string fileName)
    {
        using var stream = AssetLoader.Open(new($"avares://SynthMultiViewer/Assets/{fileName}"));
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n");
    }
}
