using Avalonia.Media;

namespace HanumanInstitute.SynthMultiViewer.Models;

/// <summary>
/// Derives subtle tab fills from a VapourSynth green or AviSynth blue hue.
/// Light editors sit near white, dark editors near black; viewers take a little more of the hue.
/// </summary>
public static class TabColors
{
    /// <summary>
    /// Gets the VapourSynth hue mixed into tab fills.
    /// </summary>
    public static Color VapourSynth { get; } = Color.FromRgb(0x4E, 0xA8, 0x4E);

    /// <summary>
    /// Gets the AviSynth hue mixed into tab fills.
    /// </summary>
    public static Color AviSynth { get; } = Color.FromRgb(0x4A, 0x82, 0xC4);

    /// <summary>
    /// Returns the hue for an engine.
    /// </summary>
    public static Color Hue(ScriptKind kind) =>
        kind == ScriptKind.AviSynth ? AviSynth : VapourSynth;

    /// <summary>
    /// Returns a theme-aware fill from an engine hue, or from <paramref name="hue"/> when set.
    /// </summary>
    public static Color For(ScriptKind kind, bool viewer, AppTheme theme, Color? hue = null) =>
        Mix(hue ?? Hue(kind), viewer, theme == AppTheme.Dark);

    /// <summary>
    /// Mixes a hue toward the theme chrome so the fill stays quiet.
    /// Light lerps the hue into white chrome. Dark adds the same hue's chroma onto black
    /// chrome so editors and viewers share one scale instead of collapsing or punching.
    /// </summary>
    public static Color Mix(Color hue, bool viewer, bool dark)
    {
        var amount = dark
            ? viewer ? 0.40 : 0.036
            : viewer ? 0.207 : 0.081;
        var backdrop = dark ? Color.FromRgb(0x20, 0x20, 0x20) : Color.FromRgb(0xF3, 0xF3, 0xF3);
        return dark ? AddChroma(backdrop, hue, amount) : Lerp(backdrop, hue, amount);
    }

    /// <summary>
    /// Shifts chrome by the hue's deviation from grey. Luminance stays near black.
    /// </summary>
    private static Color AddChroma(Color chrome, Color hue, double amount)
    {
        var mean = (hue.R + hue.G + hue.B) / 3.0;
        return Color.FromRgb(
            ClampByte(chrome.R + (hue.R - mean) * amount),
            ClampByte(chrome.G + (hue.G - mean) * amount),
            ClampByte(chrome.B + (hue.B - mean) * amount));
    }

    private static Color Lerp(Color from, Color to, double t) =>
        Color.FromArgb(
            ClampByte(from.A + (to.A - from.A) * t),
            ClampByte(from.R + (to.R - from.R) * t),
            ClampByte(from.G + (to.G - from.G) * t),
            ClampByte(from.B + (to.B - from.B) * t));

    private static byte ClampByte(double value) =>
        (byte)Math.Clamp(Math.Round(value), 0, 255);
}
