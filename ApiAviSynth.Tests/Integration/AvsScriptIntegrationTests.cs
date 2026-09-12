using System.Runtime.InteropServices;
using Xunit;

namespace HanumanInstitute.ApiAviSynth.Tests;

[CollectionDefinition("AviSynthNative", DisableParallelization = true)]
public class AviSynthNativeCollection;

[Collection("AviSynthNative")]
public class AvsScriptIntegrationTests
{
    private static readonly Lazy<bool> NativeAvailable = new(static () =>
    {
        foreach (var candidate in AvsPathResolver.GetLibraryCandidates())
        {
            if (NativeLibrary.TryLoad(candidate, out var library))
            {
                NativeLibrary.Free(library);
                return true;
            }
        }

        return false;
    });

    [Fact]
    public void LoadScript_BlankClip_ReturnsVideoInfo()
    {
        SkipIfNativeUnavailable();

        using var script = AvsScript.LoadScript("BlankClip(length=12, width=160, height=120)\n");
        var info = script.VideoInfo;
        using var frame = script.GetFrame(0);
        var plane = frame.GetPlane(0);

        Assert.Equal(160, info.Width);
        Assert.Equal(120, info.Height);
        Assert.Equal(12, info.FrameCount);
        Assert.NotEqual(IntPtr.Zero, plane.Pointer);
        Assert.Equal(120, plane.Height);
        Assert.True(plane.RowSize >= 160 * 4);
    }

    [Fact]
    public void LoadFile_BlankClip_ReturnsVideoInfo()
    {
        SkipIfNativeUnavailable();
        var path = WriteTempScript("BlankClip(length=10, width=320, height=240)\n");

        try
        {
            using var script = AvsScript.LoadFile(path);
            var info = script.VideoInfo;

            Assert.Equal(320, info.Width);
            Assert.Equal(240, info.Height);
            Assert.Equal(10, info.FrameCount);
            Assert.True(info.FpsNumerator > 0);
            Assert.True(info.FpsDenominator > 0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GetFrame_FirstFrame_ReturnsReadablePlane()
    {
        SkipIfNativeUnavailable();
        var path = WriteTempScript("BlankClip(length=3, width=160, height=120, pixel_type=\"RGB24\")\n");

        try
        {
            using var script = AvsScript.LoadFile(path);
            using var frame = script.GetFrame(0);
            var plane = frame.GetPlane(0);

            Assert.NotEqual(IntPtr.Zero, plane.Pointer);
            Assert.Equal(120, plane.Height);
            Assert.True(plane.RowSize >= 160 * 3);
            Assert.True(plane.Stride >= plane.RowSize);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadFile_MissingFile_ThrowsAvsException()
    {
        SkipIfNativeUnavailable();
        var path = Path.Combine(Path.GetTempPath(), $"SynthMultiViewer-missing-{Guid.NewGuid():N}.avs");

        var action = () => AvsScript.LoadFile(path);

        Assert.Throws<AvsException>(action);
    }

    [Fact]
    public void LoadFile_InvalidScript_ThrowsAvsException()
    {
        SkipIfNativeUnavailable();
        var path = WriteTempScript("this is not a valid avisynth script\n");

        try
        {
            var action = () => AvsScript.LoadFile(path);

            Assert.Throws<AvsException>(action);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Clip_LoadedScript_ReturnsNonZeroHandle()
    {
        SkipIfNativeUnavailable();
        var path = WriteTempScript("BlankClip()\n");

        try
        {
            using var script = AvsScript.LoadFile(path);

            Assert.NotEqual(IntPtr.Zero, script.Clip);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void SkipIfNativeUnavailable() =>
        Assert.SkipUnless(NativeAvailable.Value, "AviSynth+ native library was not found.");

    private static string WriteTempScript(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"SynthMultiViewer-{Guid.NewGuid():N}.avs");
        File.WriteAllText(path, contents);
        return path;
    }
}
