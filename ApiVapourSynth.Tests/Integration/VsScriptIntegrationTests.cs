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
    public void LoadScript_YuvFilterRejectsRgb_EvaluatesThenConvertsForDisplay()
    {
        SkipIfNativeUnavailable();
        var scriptText = """
            import vapoursynth as vs
            core = vs.core
            clip = core.std.BlankClip(width=32, height=32, length=2, format=vs.YUV420P8)
            if clip.format.color_family not in (vs.YUV, vs.GRAY) or clip.format.bits_per_sample > 16:
                raise vs.Error("input clip must be GRAY, 420, 422, 444, up to 16bits, with constant dimensions")
            clip.set_output()
            """;

        using var script = VsScript.LoadScript(scriptText);
        using var output = script.GetOutput();
        using var frame = output.GetFrame(0);

        Assert.Equal(VsColorFamily.RGB, output.VideoInfo.Format.ColorFamily);
        Assert.Equal(8, output.VideoInfo.Format.BitsPerSample);
        Assert.NotEqual(IntPtr.Zero, frame.GetPlane(0).Ptr);
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
    public void LoadScript_WithoutPath_EvaluatesBufferWithoutAWorkingDirectory()
    {
        SkipIfNativeUnavailable();
        var scriptText = """
            import vapoursynth as vs
            if "__file__" in globals():
                raise vs.Error("unsaved evaluation must not invent __file__")
            clip = vs.core.std.BlankClip(width=16, height=16, length=1, format=vs.RGB24)
            clip.set_output()
            """;

        using var script = VsScript.LoadScript(scriptText);
        using var output = script.GetOutput();

        Assert.Equal(16, output.VideoInfo.Width);
        Assert.Equal(16, output.VideoInfo.Height);
    }

    [Fact]
    public void LoadScript_WithMissingPath_DefinesFileAndEvaluatesBuffer()
    {
        SkipIfNativeUnavailable();
        var directory = Path.Combine(Path.GetTempPath(), $"SynthMultiViewer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.GetFullPath(Path.Combine(directory, "missing.vpy"));
        var scriptText = $"""
            import vapoursynth as vs
            import os
            if os.path.normpath(__file__) != os.path.normpath(r"{path}"):
                raise vs.Error("__file__ is not the supplied path")
            if os.path.isfile(__file__):
                raise vs.Error("buffer evaluation must not require the path to exist")
            clip = vs.core.std.BlankClip(width=16, height=16, length=1, format=vs.RGB24)
            clip.set_output()
            """;

        try
        {
            using var script = VsScript.LoadScript(scriptText, path);
            using var output = script.GetOutput();

            Assert.Equal(16, output.VideoInfo.Width);
            Assert.False(File.Exists(path));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void LoadScript_WithPath_DefinesFileAndWorkingDirectory()
    {
        SkipIfNativeUnavailable();
        var directory = Path.Combine(Path.GetTempPath(), $"SynthMultiViewer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "script.vpy");
        File.WriteAllText(Path.Combine(directory, "marker.txt"), "ok");
        File.WriteAllText(path, $$"""
            import vapoursynth as vs
            import os
            if os.path.normpath(__file__) != os.path.normpath(r"{{path}}"):
                raise vs.Error("__file__ is not the script path")
            with open("marker.txt", encoding="utf-8") as marker:
                if marker.read() != "ok":
                    raise vs.Error("working directory is not the script directory")
            clip = vs.core.std.BlankClip(width=16, height=16, length=1, format=vs.RGB24)
            clip.set_output()
            """);

        try
        {
            using var script = VsScript.LoadScript(File.ReadAllText(path), path);
            using var output = script.GetOutput();

            Assert.Equal(16, output.VideoInfo.Width);
            Assert.Equal(16, output.VideoInfo.Height);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
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
    public void ClearQueue_SlowFrameInFlight_DisposeDoesNotDeadlock()
    {
        SkipIfNativeUnavailable();

        const string slow = """
            import vapoursynth as vs
            import time
            core = vs.core
            src = core.std.BlankClip(width=16, height=16, length=5, format=vs.RGB24)
            def slow(n):
                time.sleep(1.5)
                return src
            clip = core.std.FrameEval(src, slow)
            clip.set_output()
            """;
        var script = VsScript.LoadScript(slow);
        var output = script.GetOutput();
        var released = new ManualResetEventSlim(false);
        output.GetFrameAsync(0);
        var queuedUntil = DateTime.UtcNow.AddSeconds(2);
        while (output.GetQueueLength(VsFrameState.Requested) == 0 && DateTime.UtcNow < queuedUntil)
        {
            Thread.Sleep(1);
        }

        Assert.True(output.GetQueueLength(VsFrameState.Requested) > 0);
        var started = DateTime.UtcNow;
        output.ClearQueue(() =>
        {
            output.Dispose();
            script.Dispose();
            released.Set();
        });

        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(1),
            "ClearQueue blocked the caller while a VapourSynth frame was still in flight.");
        Assert.True(released.Wait(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));
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
