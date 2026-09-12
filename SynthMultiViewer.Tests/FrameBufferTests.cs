using System.Runtime.InteropServices;
using HanumanInstitute.MediaSynthUI;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class FrameBufferTests
{
    [Fact]
    public void CopyTo_NativeFrameReleased_PreservesPixelsAndRowPadding()
    {
        byte[] nativeRows = [1, 2, 3, 4, 99, 99, 5, 6, 7, 8, 99, 99];
        using var destination = new NativeBuffer(16);
        using var pixels = CopyNativeFrame(nativeRows);
        var actual = new byte[16];

        pixels.CopyTo(destination.Address, 8);
        Marshal.Copy(destination.Address, actual, 0, actual.Length);

        Assert.Equal([1, 2, 3, 4, 0, 0, 0, 0, 5, 6, 7, 8, 0, 0, 0, 0], actual);
    }

    [Fact]
    public void CopyFrom_NegativeStride_FlipsRowsToTopDown()
    {
        byte[] nativeRows = [10, 11, 12, 13, 99, 99, 20, 21, 22, 23, 99, 99];
        using var source = new NativeBuffer(nativeRows.Length);
        Marshal.Copy(nativeRows, 0, source.Address, nativeRows.Length);
        using var destination = new NativeBuffer(8);
        var actual = new byte[8];

        using var pixels = FrameBuffer.CopyFrom(source.Address + 6, -6, 4, 2);
        pixels.CopyTo(destination.Address, 4);
        Marshal.Copy(destination.Address, actual, 0, actual.Length);

        Assert.Equal([10, 11, 12, 13, 20, 21, 22, 23], actual);
    }

    [Fact]
    public void CopyRgbToBgra_PlanarRgb_ProducesOpaqueBgraPixels()
    {
        using var red = new NativeBuffer(2);
        using var green = new NativeBuffer(2);
        using var blue = new NativeBuffer(2);
        using var destination = new NativeBuffer(8);
        Marshal.Copy(new byte[] { 10, 20 }, 0, red.Address, 2);
        Marshal.Copy(new byte[] { 30, 40 }, 0, green.Address, 2);
        Marshal.Copy(new byte[] { 50, 60 }, 0, blue.Address, 2);

        using var pixels = FrameBuffer.CopyRgbToBgra(red.Address, 2, green.Address, 2, blue.Address, 2, 2, 1);
        var actual = new byte[8];
        pixels.CopyTo(destination.Address, 8);
        Marshal.Copy(destination.Address, actual, 0, actual.Length);

        Assert.Equal([50, 30, 10, 255, 60, 40, 20, 255], actual);
    }

    [Fact]
    public void CopyTo_BufferDisposed_RejectsAccessToReturnedPixels()
    {
        using var pixels = CopyNativeFrame(new byte[12]);
        using var destination = new NativeBuffer(16);

        pixels.Dispose();
        var error = Record.Exception(() => pixels.CopyTo(destination.Address, 8));

        Assert.IsType<ObjectDisposedException>(error);
    }

    private static FrameBuffer CopyNativeFrame(byte[] rows)
    {
        using var source = new NativeBuffer(rows.Length);
        Marshal.Copy(rows, 0, source.Address, rows.Length);
        return FrameBuffer.CopyFrom(source.Address, 6, 4, 2);
    }

    private sealed class NativeBuffer : IDisposable
    {
        public NativeBuffer(int size)
        {
            Address = Marshal.AllocHGlobal(size);
            Marshal.Copy(new byte[size], 0, Address, size);
        }

        public IntPtr Address { get; }
        public void Dispose() => Marshal.FreeHGlobal(Address);
    }
}
