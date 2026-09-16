using Avalonia.Media;

namespace HanumanInstitute.SynthMultiViewer.Models;

/// <summary>
/// Tab fills from two dark editor colors. Viewers scale RGB by brightness; light is 255 minus that.
/// </summary>
public static class TabColors
{
    /// <summary>Dark VapourSynth editor fill.</summary>
    public static Color VapourSynth { get; } = Color.FromRgb(28, 16, 20);
    /// <summary>Dark AviSynth editor fill.</summary>
    public static Color AviSynth { get; } = Color.FromRgb(16, 20, 28);
    /// <summary>Viewer RGB scale; light uses 255 minus the dark result.</summary>
    public const double ViewerBrightness = 2.3;

    /// <summary>
    /// Returns the dark editor fill for an engine.
    /// </summary>
    public static Color Hue(ScriptKind kind) =>
        kind == ScriptKind.AviSynth ? AviSynth : VapourSynth;

    /// <summary>
    /// Returns a theme-aware fill from the engine color, or <paramref name="hue"/> unchanged when set.
    /// </summary>
    public static Color For(ScriptKind kind, bool viewer, AppTheme theme, Color? hue = null) =>
        hue is { } custom
            ? Color.FromRgb(custom.R, custom.G, custom.B)
            : Mix(Hue(kind), viewer, theme == AppTheme.Dark);

    /// <summary>
    /// Derives a fill: viewers scale the dark editor RGB, light inverts each channel from 255.
    /// </summary>
    public static Color Mix(Color hue, bool viewer, bool dark)
    {
        var color = viewer ? Scale(hue, ViewerBrightness) : hue;
        return dark ? color : Invert(color);
    }

    private static Color Scale(Color color, double factor) =>
        Color.FromRgb(
            ClampByte(color.R * factor),
            ClampByte(color.G * factor),
            ClampByte(color.B * factor));

    private static Color Invert(Color color) =>
        Color.FromRgb(
            (byte)(255 - color.R),
            (byte)(255 - color.G),
            (byte)(255 - color.B));

    private static byte ClampByte(double value) =>
        (byte)Math.Clamp(Math.Round(value), 0, 255);
}
