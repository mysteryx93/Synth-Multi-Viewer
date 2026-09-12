using System.Buffers;
using HanumanInstitute.ApiVapourSynth;

namespace HanumanInstitute.MediaSynthUI;

/// <summary>
/// Owns a pixel copy that remains valid after a native frame callback returns.
/// </summary>
internal sealed class FrameBuffer : IDisposable
{
    private byte[]? _pixels;
    private readonly int _rowSize;
    private readonly int _height;

    private FrameBuffer(byte[] pixels, int rowSize, int height)
    {
        _pixels = pixels;
        _rowSize = rowSize;
        _height = height;
    }

    /// <summary>
    /// Copies native image rows into an owned buffer, excluding row padding.
    /// AviSynth RGB is stored bottom-up; pass <paramref name="flipVertical"/> to convert to top-down.
    /// </summary>
    public static unsafe FrameBuffer CopyFrom(IntPtr source, int stride, int rowSize, int height, bool flipVertical = false)
    {
        var invertRows = flipVertical ? stride > 0 : stride < 0;
        if (invertRows && height > 0)
        {
            source = IntPtr.Add(source, checked((height - 1) * stride));
            stride = -stride;
        }

        var pixels = ArrayPool<byte>.Shared.Rent(checked(rowSize * height));
        try
        {
            fixed (byte* target = pixels)
            {
                VsHelper.BitBlt((IntPtr)target, rowSize, source, stride, rowSize, height);
            }
            return new FrameBuffer(pixels, rowSize, height);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(pixels);
            throw;
        }
    }

    /// <summary>
    /// Copies planar RGB rows into an owned BGRA buffer.
    /// </summary>
    public static unsafe FrameBuffer CopyRgbToBgra(
        IntPtr red, int redStride, IntPtr green, int greenStride, IntPtr blue, int blueStride, int width, int height)
    {
        var rowSize = checked(width * 4);
        var pixels = ArrayPool<byte>.Shared.Rent(checked(rowSize * height));
        try
        {
            fixed (byte* target = pixels)
            {
                for (var y = 0; y < height; y++)
                {
                    var sourceRed = (byte*)red + y * redStride;
                    var sourceGreen = (byte*)green + y * greenStride;
                    var sourceBlue = (byte*)blue + y * blueStride;
                    var destination = target + y * rowSize;
                    for (var x = 0; x < width; x++)
                    {
                        destination[x * 4] = sourceBlue[x];
                        destination[x * 4 + 1] = sourceGreen[x];
                        destination[x * 4 + 2] = sourceRed[x];
                        destination[x * 4 + 3] = byte.MaxValue;
                    }
                }
            }

            return new FrameBuffer(pixels, rowSize, height);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(pixels);
            throw;
        }
    }

    /// <summary>
    /// Copies the owned pixels into a bitmap with the specified row stride.
    /// </summary>
    public unsafe void CopyTo(IntPtr target, int stride)
    {
        ObjectDisposedException.ThrowIf(_pixels == null, this);
        fixed (byte* source = _pixels)
        {
            VsHelper.BitBlt(target, stride, (IntPtr)source, _rowSize, _rowSize, _height);
        }
    }

    /// <summary>
    /// Returns the pixel buffer to the shared pool.
    /// </summary>
    public void Dispose()
    {
        var pixels = Interlocked.Exchange(ref _pixels, null);
        if (pixels != null)
        {
            ArrayPool<byte>.Shared.Return(pixels);
        }
    }
}
