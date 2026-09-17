using System.Text.RegularExpressions;

namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Regular expressions for VapourSynth buffer bindings.
/// </summary>
internal static partial class VapourSynthPatterns
{
    [GeneratedRegex(@"^def\s+([\p{L}_][\p{L}\p{N}_]*)\s*\(", RegexOptions.Multiline)]
    public static partial Regex TopLevelDef();
}
