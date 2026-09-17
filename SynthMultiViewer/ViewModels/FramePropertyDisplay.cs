namespace HanumanInstitute.SynthMultiViewer.ViewModels;

/// <summary>
/// Turns reserved VapourSynth / AviSynth+ frame-property values into display text.
/// Unknown and custom keys are left unchanged.
/// </summary>
public static class FramePropertyDisplay
{
    /// <summary>
    /// Returns a readable value for a reserved key, or the raw value for anything else.
    /// </summary>
    public static string Format(string name, string value) => name switch
    {
        "_Matrix" => Named(Matrix(value), value),
        "_Transfer" => Named(Transfer(value), value),
        "_Primaries" => Named(Primaries(value), value),
        "_ChromaLocation" => Named(ChromaLocation(value), value),
        "_FieldBased" => Named(FieldBased(value), value),
        "_Field" => Named(Field(value), value),
        "_ColorRange" => Named(ColorRange(value), value),
        "_Range" => Named(Range(value), value),
        "_PictType" => Named(PictType(value), value),
        "_Combed" or "_SceneChangeNext" or "_SceneChangePrev" => Named(Flag(value), value),
        _ => value
    };

    private static string Named(string? label, string raw) =>
        !label.HasValue() || label == raw ? raw : raw + " (" + label + ")";

    private static string? Matrix(string value) => value switch
    {
        "0" => "RGB",
        "1" => "BT.709",
        "2" => "Unspecified",
        "4" => "FCC",
        "5" => "BT.470BG",
        "6" => "BT.601",
        "7" => "ST240M",
        "8" => "YCgCo",
        "9" => "BT.2020 NCL",
        "10" => "BT.2020 CL",
        "11" => "ST2085",
        "12" => "Chromaticity-derived NCL",
        "13" => "Chromaticity-derived CL",
        "14" => "ICtCp",
        _ => null
    };

    private static string? Transfer(string value) => value switch
    {
        "1" => "BT.709",
        "2" => "Unspecified",
        "4" => "BT.470M",
        "5" => "BT.470BG",
        "6" => "BT.601",
        "7" => "ST240M",
        "8" => "Linear",
        "9" => "Log 100",
        "10" => "Log 316",
        "11" => "IEC 61966-2-4",
        "13" => "sRGB",
        "14" => "BT.2020 10-bit",
        "15" => "BT.2020 12-bit",
        "16" => "ST2084 (PQ)",
        "17" => "ST428",
        "18" => "HLG",
        _ => null
    };

    private static string? Primaries(string value) => value switch
    {
        "1" => "BT.709",
        "2" => "Unspecified",
        "4" => "BT.470M",
        "5" => "BT.470BG",
        "6" => "ST170M",
        "7" => "ST240M",
        "8" => "Film",
        "9" => "BT.2020",
        "10" => "ST428",
        "11" => "DCI-P3",
        "12" => "Display P3",
        "22" => "EBU 3213",
        _ => null
    };

    private static string? ChromaLocation(string value) => value switch
    {
        "0" => "Left",
        "1" => "Center",
        "2" => "Top-left",
        "3" => "Top",
        "4" => "Bottom-left",
        "5" => "Bottom",
        _ => null
    };

    private static string? FieldBased(string value) => value switch
    {
        "0" => "Progressive",
        "1" => "Bottom field first",
        "2" => "Top field first",
        _ => null
    };

    private static string? Field(string value) => value switch
    {
        "0" => "Bottom",
        "1" => "Top",
        _ => null
    };

    // AviSynth+ and older VS: 0=full, 1=limited. VS _Range (R4.2) is the inverse (H.273).
    private static string? ColorRange(string value) => value switch
    {
        "0" => "Full",
        "1" => "Limited",
        _ => null
    };

    private static string? Range(string value) => value switch
    {
        "0" => "Limited",
        "1" => "Full",
        _ => null
    };

    private static string? PictType(string value) => value switch
    {
        "I" => "Intra",
        "P" => "Predicted",
        "B" => "Bidirectional",
        _ => null
    };

    private static string? Flag(string value) => value switch
    {
        "0" => "No",
        "1" => "Yes",
        _ => null
    };
}
