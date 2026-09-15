using System.Runtime.InteropServices;

namespace HanumanInstitute.ApiAviSynth;

/// <summary>
/// Describes an AviSynth video stream.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct AvsVideoInfo
{
    /// <summary>
    /// Gets the frame width.
    /// </summary>
    public int Width;

    /// <summary>
    /// Gets the frame height.
    /// </summary>
    public int Height;

    /// <summary>
    /// Gets the frame-rate numerator.
    /// </summary>
    public uint FpsNumerator;

    /// <summary>
    /// Gets the frame-rate denominator.
    /// </summary>
    public uint FpsDenominator;

    /// <summary>
    /// Gets the number of frames.
    /// </summary>
    public int FrameCount;

    /// <summary>
    /// Gets the AviSynth pixel-format identifier.
    /// </summary>
    public int PixelType;

    /// <summary>
    /// Gets the script pixel-type name, such as YUV420P10.
    /// </summary>
    public readonly string FormatName => AvsPixelFormat.GetName(PixelType);
}
