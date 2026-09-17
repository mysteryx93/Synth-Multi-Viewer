using HanumanInstitute.ScriptAssist.AviSynth;
using HanumanInstitute.ScriptAssist.VapourSynth;
using HanumanInstitute.SynthMultiViewer.Models;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Hosts script-assist catalogs and the editor-enhancement setting.
/// </summary>
public sealed class ScriptAssistService : IScriptLanguageFactory
{
    private readonly ISettingsProvider<AppSettingsData> _settings;
    private readonly ScriptLanguageFactory _factory;

    /// <summary>
    /// Creates the factory around native plugin catalogs and live settings.
    /// </summary>
    public ScriptAssistService(ISettingsProvider<AppSettingsData> settings)
    {
        _settings = settings;
        _factory = new ScriptLanguageFactory(
            [
                new LanguageProfile(
                    nameof(ScriptKind.VapourSynth),
                    new VapourSynthLanguage(ScriptIncludeIO.VapourSynth),
                    new CatalogCache(ScriptCatalogs.VapourSynth)),
                new LanguageProfile(
                    nameof(ScriptKind.AviSynth),
                    new AviSynthLanguage(ScriptIncludeIO.AviSynth),
                    new CatalogCache(ScriptCatalogs.AviSynth))
            ],
            () => _settings.Value.EnhanceEditorWithAutoComplete);
    }

    /// <inheritdoc />
    public bool IsEnabled => _factory.IsEnabled;

    /// <inheritdoc />
    public ILanguageService? Create(string language) => _factory.Create(language);

    /// <inheritdoc />
    public void Configure(string language, string catalogKey) => _factory.Configure(language, catalogKey);

    /// <inheritdoc />
    public void Refresh() => _factory.Refresh();
}
