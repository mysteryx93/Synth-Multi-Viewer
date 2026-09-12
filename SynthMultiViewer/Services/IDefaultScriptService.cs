namespace HanumanInstitute.SynthMultiViewer.Services;

/// <summary>
/// Provides the built-in default scripts used for new editor tabs.
/// </summary>
public interface IDefaultScriptService
{
    /// <summary>
    /// Gets the default VapourSynth script text.
    /// </summary>
    string VapourSynth { get; }

    /// <summary>
    /// Gets the default AviSynth script text.
    /// </summary>
    string AviSynth { get; }
}
