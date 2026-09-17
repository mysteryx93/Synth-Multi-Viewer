using HanumanInstitute.SynthMultiViewer.Models;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Script assistance wired to this app’s native catalogs and include readers.
/// </summary>
public sealed class ScriptAssistService : ScriptLanguageFactory
{
    /// <summary>
    /// Creates the factory around native catalogs and the editor-enhancement setting.
    /// </summary>
    public ScriptAssistService(ISettingsProvider<AppSettingsData> settings)
        : base(
            ScriptCatalogs.VapourSynth,
            ScriptCatalogs.AviSynth,
            ScriptIncludeIO.VapourSynth,
            ScriptIncludeIO.AviSynth)
    {
        IsEnabled = settings.Value.EnhanceEditorWithAutoComplete;
    }
}
