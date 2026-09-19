namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// One plugin function copied from a throwaway VapourSynth core.
/// </summary>
public sealed record VapourSynthFunction(
    string Namespace,
    string Name,
    string? Arguments,
    string? ReturnType = null,
    string? PluginName = null);
