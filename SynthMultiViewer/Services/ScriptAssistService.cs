using HanumanInstitute.ScriptAssist.Services;
using HanumanInstitute.SynthMultiViewer.Models;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Script assistance wired to this app’s native catalogs and include sources.
/// </summary>
public sealed class ScriptAssistService : ScriptLanguageFactory
{
    /// <summary>
    /// Creates the factory around native catalogs and the editor-enhancement setting.
    /// </summary>
    public ScriptAssistService(ISettingsProvider<AppSettingsData> settings, IFileSystemService files)
        : base(
            new VapourSynthNativeCatalog(),
            new AviSynthNativeCatalog(),
            new AviSynthPluginDirectory(files),
            new VapourSynthIncludeSource(files),
            new AviSynthIncludeSource(files))
    {
        IsEnabled = settings.Value.EnhanceEditorWithAutoComplete;
    }

    /// <inheritdoc />
    public override void Refresh()
    {
        VapourSynthIncludeSource.Invalidate();
        base.Refresh();
    }
}
