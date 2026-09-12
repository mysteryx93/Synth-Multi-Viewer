namespace HanumanInstitute.MediaSynthUI;

/// <summary>
/// Identifies which native script engine should load a script.
/// </summary>
public enum ScriptKind
{
    /// <summary>
    /// A VapourSynth Python script.
    /// </summary>
    VapourSynth,

    /// <summary>
    /// An AviSynth+ script.
    /// </summary>
    AviSynth
}
