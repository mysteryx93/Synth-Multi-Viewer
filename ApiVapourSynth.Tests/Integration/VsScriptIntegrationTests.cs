using System.Runtime.InteropServices;
using Xunit;

namespace HanumanInstitute.ApiVapourSynth.Tests;

[CollectionDefinition("VapourSynthNative", DisableParallelization = true)]
public class VapourSynthNativeCollection;

[Collection("VapourSynthNative")]
public class VsScriptIntegrationTests
{
    private static readonly Lazy<bool> NativeAvailable = new(static () =>
    {
        try
        {
            using var script = VsScript.CreateEmpty();
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    });

    private const string RgbBlankClip = """
        import vapoursynth as vs
        core = vs.core
        clip = core.std.BlankClip(width=320, height=240, length=5, format=vs.RGB24, color=[10, 20, 30])
        clip.set_output()
        """;

    private const string YuvBlankClip = """
        import vapoursynth as vs
        core = vs.core
        clip = core.std.BlankClip(width=320, height=240, length=5, format=vs.YUV420P8)
        clip.set_output()
        """;

    [Fact]
    public void CreateEmpty_NativeLibrary_CreatesScriptEnvironment()
    {
        SkipIfNativeUnavailable();

        using var script = VsScript.CreateEmpty();

        Assert.NotNull(script);
    }

    [Fact]
    public void LoadScript_BlankClipRgb_ReturnsRgb24Frame()
    {
        SkipIfNativeUnavailable();

        using var script = VsScript.LoadScript(RgbBlankClip);
        using var output = script.GetOutput();
        var info = output.VideoInfo;
        using var frame = output.GetFrame(0);
        var red = frame.GetPlane(0);

        Assert.Equal(320, info.Width);
        Assert.Equal(240, info.Height);
        Assert.Equal(5, info.NumFrames);
        Assert.Equal(VsColorFamily.RGB, info.Format.ColorFamily);
        Assert.Equal(3, info.Format.NumPlanes);
        Assert.Equal(8, info.Format.BitsPerSample);
        Assert.Equal(1, info.Format.BytesPerSample);
        Assert.Equal(320, red.Width);
        Assert.Equal(240, red.Height);
        Assert.NotEqual(IntPtr.Zero, red.Ptr);
        Assert.Equal(10, Marshal.ReadByte(red.Ptr));
    }

    [Fact]
    public void LoadScript_BlankClipYuv_ConvertsToRgb24()
    {
        SkipIfNativeUnavailable();

        using var script = VsScript.LoadScript(YuvBlankClip);
        using var output = script.GetOutput();
        var info = output.VideoInfo;
        using var frame = output.GetFrame(0);
        var red = frame.GetPlane(0);

        Assert.Equal(320, info.Width);
        Assert.Equal(240, info.Height);
        Assert.Equal(VsColorFamily.RGB, info.Format.ColorFamily);
        Assert.Equal(3, info.Format.NumPlanes);
        Assert.Equal(8, info.Format.BitsPerSample);
        Assert.Equal(320, red.Width);
        Assert.NotEqual(IntPtr.Zero, red.Ptr);
    }

    [Fact]
    public void LoadScript_MissingOutput_ThrowsVsExceptionWithOutputMessage()
    {
        SkipIfNativeUnavailable();
        var scriptText = "import vapoursynth as vs\ncore = vs.core\n";

        var action = () => VsScript.LoadScript(scriptText);

        var error = Assert.Throws<VsException>(action);
        Assert.Contains("did not set video output", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Python exception: 0", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadFile_TempYuvScript_ConvertsToRgb24()
    {
        SkipIfNativeUnavailable();
        var path = WriteTempScript(".vpy", YuvBlankClip);

        try
        {
            using var script = VsScript.LoadFile(path);
            using var output = script.GetOutput();
            var info = output.VideoInfo;
            using var frame = output.GetFrame(0);

            Assert.Equal(VsColorFamily.RGB, info.Format.ColorFamily);
            Assert.Equal(320, info.Width);
            Assert.NotEqual(IntPtr.Zero, frame.GetPlane(0).Ptr);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadFileDirect_YuvScript_KeepsYuvFormat()
    {
        SkipIfNativeUnavailable();
        var path = WriteTempScript(".vpy", YuvBlankClip);

        try
        {
            using var script = VsScript.LoadFileDirect(path, true);
            using var output = script.GetOutput();
            var info = output.VideoInfo;
            using var frame = output.GetFrame(0);
            var luma = frame.GetPlane(0);

            Assert.Equal(VsColorFamily.YUV, info.Format.ColorFamily);
            Assert.Equal(320, info.Width);
            Assert.Equal(240, info.Height);
            Assert.Equal(320, luma.Width);
            Assert.Equal(240, luma.Height);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GetOutput_MissingIndex_ThrowsVsException()
    {
        SkipIfNativeUnavailable();

        using var script = VsScript.LoadScript(RgbBlankClip);
        using var output = script.GetOutput();
        var action = () => script.GetOutput(1);

        var error = Assert.Throws<VsException>(action);
        Assert.Contains("did not set the requested video output", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(320, output.VideoInfo.Width);
    }

    [Fact]
    public void GetFrameAsync_FirstFrame_RaisesFrameReady()
    {
        SkipIfNativeUnavailable();
        using var ready = new ManualResetEventSlim(false);
        VsFrameStatus? status = null;

        using var script = VsScript.LoadScript(RgbBlankClip);
        using var output = script.GetOutput();
        output.FrameReady += (_, e) =>
        {
            status = e;
            ready.Set();
        };
        output.GetFrameAsync(0);

        Assert.True(ready.Wait(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));
        Assert.NotNull(status);
        Assert.Equal(0, status.Index);
        Assert.Equal(VsFrameState.Completed, status.State);
    }

    [Fact]
    public void SetThreadCount_PositiveValue_ReturnsReportedCount()
    {
        SkipIfNativeUnavailable();

        using var script = VsScript.LoadScript(RgbBlankClip);
        using var output = script.GetOutput();

        var threads = output.SetThreadCount(2);

        Assert.True(threads >= 1);
    }

    private static void SkipIfNativeUnavailable() =>
        Assert.SkipUnless(NativeAvailable.Value, "VapourSynth native library was not found.");

    private static string WriteTempScript(string extension, string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"SynthMultiViewer-{Guid.NewGuid():N}{extension}");
        File.WriteAllText(path, contents);
        return path;
    }
}
