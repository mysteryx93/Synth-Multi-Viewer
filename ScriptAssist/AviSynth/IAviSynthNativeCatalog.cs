namespace HanumanInstitute.ScriptAssist.AviSynth;

/// <summary>
/// Copies autoload metadata from a throwaway AviSynth environment.
/// </summary>
public interface IAviSynthNativeCatalog
{
    /// <summary>
    /// Enumerates Internal, Plugin, and User functions. Does not evaluate editor text.
    /// </summary>
    IReadOnlyList<AviSynthFilter> Read();
}
