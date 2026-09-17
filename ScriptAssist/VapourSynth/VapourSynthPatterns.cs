using System.Text.RegularExpressions;

namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Regular expressions for VapourSynth buffer bindings.
/// </summary>
internal static partial class VapourSynthPatterns
{
    [GeneratedRegex(@"\bimport\s+vapoursynth\s+as\s+([\p{L}_][\p{L}\p{N}_]*)")]
    public static partial Regex ImportVapourSynth();

    [GeneratedRegex(@"\bfrom\s+vapoursynth\s+import\s+core(?:\s+as\s+([\p{L}_][\p{L}\p{N}_]*))?")]
    public static partial Regex FromVapourSynthCore();

    [GeneratedRegex(@"^\s*([\p{L}_][\p{L}\p{N}_\p{M}]*)\s*(?::\s*([^\n=]+))?\s*=(?!=)\s*(.*)$", RegexOptions.Multiline)]
    public static partial Regex NameAssign();

    [GeneratedRegex(@"^\s*([\p{L}_][\p{L}\p{N}_\p{M}]*(?:\s*,\s*[\p{L}_][\p{L}\p{N}_\p{M}]*)+)\s*=", RegexOptions.Multiline)]
    public static partial Regex UnpackAssign();

    [GeneratedRegex(@"^\s*import\s+([\p{L}_][\p{L}\p{N}_.]*)(?:\s+as\s+([\p{L}_][\p{L}\p{N}_]*))?", RegexOptions.Multiline)]
    public static partial Regex ImportAs();

    [GeneratedRegex(@"^\s*from\s+(\.+[\p{L}_][\p{L}\p{N}_.]*|\.+|[\p{L}_][\p{L}\p{N}_.]*)\s+import\s+([^\n#]+)",
        RegexOptions.Multiline)]
    public static partial Regex FromImport();

    [GeneratedRegex(@"^def\s+([\p{L}_][\p{L}\p{N}_]*)\s*\(", RegexOptions.Multiline)]
    public static partial Regex TopLevelDef();

    [GeneratedRegex(@"^\s*def\s+([\p{L}_][\p{L}\p{N}_]*)\s*\(", RegexOptions.Multiline)]
    public static partial Regex AnyDef();
}
