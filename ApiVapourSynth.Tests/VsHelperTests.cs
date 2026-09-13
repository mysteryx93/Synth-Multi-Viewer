using System.Runtime.InteropServices;
using Xunit;

namespace HanumanInstitute.ApiVapourSynth.Tests;

public class VsHelperTests
{
    [Fact]
    public void BitBlt_PaddedRows_CopiesRowBytesOnly()
    {
        byte[] sourceRows = [1, 2, 3, 4, 99, 99, 5, 6, 7, 8, 99, 99];
        var destination = new byte[16];
        var source = Marshal.AllocHGlobal(sourceRows.Length);
        var target = Marshal.AllocHGlobal(destination.Length);
        Marshal.Copy(sourceRows, 0, source, sourceRows.Length);
        Marshal.Copy(destination, 0, target, destination.Length);

        try
        {
            VsHelper.BitBlt(target, 8, source, 6, 4, 2);
            Marshal.Copy(target, destination, 0, destination.Length);
        }
        finally
        {
            Marshal.FreeHGlobal(source);
            Marshal.FreeHGlobal(target);
        }

        Assert.Equal([1, 2, 3, 4, 0, 0, 0, 0, 5, 6, 7, 8, 0, 0, 0, 0], destination);
    }

    [Fact]
    public void GetApiVersion_Default_ReturnsApi4()
    {
        var version = VsHelper.GetApiVersion();

        Assert.Equal(4 << 16, version);
    }

    [Fact]
    public void TryFindLibrary_WhenFound_ReturnsNonEmptyPath()
    {
        var found = VsHelper.TryFindLibrary(out var path);

        if (found)
        {
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(Path.IsPathRooted(path));
            Assert.True(File.Exists(path));
        }
        else
        {
            Assert.Null(path);
        }
    }

    [Fact]
    public void SetDllPath_MissingOverride_DoesNotFallBackToSystem()
    {
        try
        {
            VsHelper.SetDllPath(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "libvsscript.so"));

            var found = VsHelper.TryFindLibrary(out var path);

            Assert.False(found);
            Assert.Null(path);
        }
        finally
        {
            VsHelper.SetDllPath("");
        }
    }

    [Fact]
    public void TryEvaluate_MissingOverride_ReturnsFalse()
    {
        try
        {
            VsHelper.SetDllPath(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "libvsscript.so"));

            var usable = VsHelper.TryEvaluate(out var error);

            Assert.False(usable);
            Assert.False(string.IsNullOrWhiteSpace(error));
        }
        finally
        {
            VsHelper.SetDllPath("");
        }
    }
}
