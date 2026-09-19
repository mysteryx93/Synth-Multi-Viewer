namespace HanumanInstitute.ScriptAssist.AviSynth;

/// <summary>
/// AviSynth functions whose return is not a clip.
/// </summary>
public static class AviSynthInternals
{
    private static readonly Dictionary<string, TypeRef> Table = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Width"] = AviSynthTypes.Int,
        ["Height"] = AviSynthTypes.Int,
        ["FrameCount"] = AviSynthTypes.Int,
        ["FrameRate"] = AviSynthTypes.Float,
        ["FrameRateNumerator"] = AviSynthTypes.Int,
        ["FrameRateDenominator"] = AviSynthTypes.Int,
        ["BitsPerComponent"] = AviSynthTypes.Int,
        ["ComponentSize"] = AviSynthTypes.Int,
        ["NumComponents"] = AviSynthTypes.Int,
        ["IsY"] = AviSynthTypes.Bool,
        ["IsY8"] = AviSynthTypes.Bool,
        ["IsYV12"] = AviSynthTypes.Bool,
        ["IsYV16"] = AviSynthTypes.Bool,
        ["IsYV24"] = AviSynthTypes.Bool,
        ["IsYV411"] = AviSynthTypes.Bool,
        ["IsYUY2"] = AviSynthTypes.Bool,
        ["IsRGB"] = AviSynthTypes.Bool,
        ["IsRGB24"] = AviSynthTypes.Bool,
        ["IsRGB32"] = AviSynthTypes.Bool,
        ["IsRGB48"] = AviSynthTypes.Bool,
        ["IsRGB64"] = AviSynthTypes.Bool,
        ["IsYUV"] = AviSynthTypes.Bool,
        ["Is420"] = AviSynthTypes.Bool,
        ["Is422"] = AviSynthTypes.Bool,
        ["Is444"] = AviSynthTypes.Bool,
        ["IsPacked"] = AviSynthTypes.Bool,
        ["IsPlanar"] = AviSynthTypes.Bool,
        ["IsInterleaved"] = AviSynthTypes.Bool,
        ["IsFieldBased"] = AviSynthTypes.Bool,
        ["IsFrameBased"] = AviSynthTypes.Bool,
        ["AverageLuma"] = AviSynthTypes.Float,
        ["AverageChromaU"] = AviSynthTypes.Float,
        ["AverageChromaV"] = AviSynthTypes.Float,
        ["AverageR"] = AviSynthTypes.Float,
        ["AverageG"] = AviSynthTypes.Float,
        ["AverageB"] = AviSynthTypes.Float,
        ["YDifferenceFromPrevious"] = AviSynthTypes.Float,
        ["YDifferenceToNext"] = AviSynthTypes.Float,
        ["RGBDifference"] = AviSynthTypes.Float,
        ["LumaDifference"] = AviSynthTypes.Float,
        ["ChromaUDifference"] = AviSynthTypes.Float,
        ["ChromaVDifference"] = AviSynthTypes.Float,
        ["Defined"] = AviSynthTypes.Bool,
        ["Exist"] = AviSynthTypes.Bool,
        ["Default"] = TypeRef.Unknown,
        ["Eval"] = TypeRef.Unknown,
        ["Import"] = TypeRef.Unknown,
        ["Assert"] = AviSynthTypes.Bool,
        ["Int"] = AviSynthTypes.Int,
        ["Float"] = AviSynthTypes.Float,
        ["String"] = AviSynthTypes.String,
        ["Value"] = AviSynthTypes.Float,
        ["Abs"] = AviSynthTypes.Float,
        ["Sign"] = AviSynthTypes.Int,
        ["Sqrt"] = AviSynthTypes.Float,
        ["Rand"] = AviSynthTypes.Int,
        ["Pi"] = AviSynthTypes.Float,
        ["Sin"] = AviSynthTypes.Float,
        ["Cos"] = AviSynthTypes.Float,
        ["Tan"] = AviSynthTypes.Float,
        ["Asin"] = AviSynthTypes.Float,
        ["Acos"] = AviSynthTypes.Float,
        ["Atan"] = AviSynthTypes.Float,
        ["Atan2"] = AviSynthTypes.Float,
        ["Exp"] = AviSynthTypes.Float,
        ["Log"] = AviSynthTypes.Float,
        ["Log10"] = AviSynthTypes.Float,
        ["Pow"] = AviSynthTypes.Float,
        ["Floor"] = AviSynthTypes.Int,
        ["Ceil"] = AviSynthTypes.Int,
        ["Round"] = AviSynthTypes.Int,
        ["Min"] = AviSynthTypes.Float,
        ["Max"] = AviSynthTypes.Float,
        ["VersionNumber"] = AviSynthTypes.Float,
        ["VersionString"] = AviSynthTypes.String,
        ["PixelType"] = AviSynthTypes.String
    };

    /// <summary>
    /// Infers the return of a called function. Unknown names default to clip.
    /// </summary>
    public static TypeRef ReturnOf(string name)
    {
        if (Table.TryGetValue(name, out var type))
        {
            return type;
        }

        return AviSynthTypes.Clip;
    }
}
