namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Copies API4 plugin metadata from a throwaway VapourSynth core.
/// </summary>
public interface IVapourSynthNativeCatalog
{
    /// <summary>
    /// Enumerates plugin functions. Does not evaluate editor text.
    /// </summary>
    IReadOnlyList<VapourSynthFunction> Read();
}
