namespace HanumanInstitute.ScriptAssist.AviSynth;

/// <summary>
/// Type identifiers used by the AviSynth profile.
/// </summary>
public static class AviSynthTypes
{
    /// <summary>A video clip.</summary>
    public static TypeRef Clip { get; } = new("clip");

    /// <summary>An integer.</summary>
    public static TypeRef Int { get; } = new("int");

    /// <summary>A floating-point number.</summary>
    public static TypeRef Float { get; } = new("float");

    /// <summary>A boolean.</summary>
    public static TypeRef Bool { get; } = new("bool");

    /// <summary>A string.</summary>
    public static TypeRef String { get; } = new("string");

    /// <summary>Gets whether <paramref name="name"/> is an AviSynth parameter type word.</summary>
    public static bool IsTypeName(string name) =>
        name.Equals("clip", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("int", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("float", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("bool", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("string", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("val", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("func", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("array", StringComparison.OrdinalIgnoreCase);

    /// <summary>Gets whether the first parameter is a clip.</summary>
    public static bool TakesClip(Symbol symbol)
    {
        var first = symbol.Parameters is { Length: > 0 } ? symbol.Parameters[0] : null;
        return first != null && first.StartsWith("clip", StringComparison.OrdinalIgnoreCase);
    }
}
