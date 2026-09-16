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

    [Fact]
    public void CatalogContainsStandardFunctionsAndApi4Arguments()
    {
        SkipIfNativeUnavailable();
        var functions = VsCatalog.Read();
        var crop = Assert.Single(functions.Where(x => x.Namespace == "std" && x.Name == "Crop"));
        Assert.Contains("clip:vnode", crop.Arguments);
    }

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

    public static TheoryData<string> DisplayFormats =>
    [
        "RGB24",
        "RGB27",
        "RGB30",
        "RGB36",
        "RGB42",
        "RGB48",
        "RGBH",
        "RGBS",
        "GRAY8",
        "GRAY9",
        "GRAY10",
        "GRAY12",
        "GRAY14",
        "GRAY16",
        "GRAY32",
        "GRAYH",
        "GRAYS",
        "YUV420P8",
        "YUV420P9",
        "YUV420P10",
        "YUV420P12",
        "YUV420P14",
        "YUV420P16",
        "YUV420PH",
        "YUV420PS",
        "YUV422P8",
        "YUV422P10",
        "YUV422P16",
        "YUV422PS",
        "YUV444P8",
        "YUV444P10",
        "YUV444P16",
        "YUV444PS",
        "YUV410P8",
        "YUV411P8",
        "YUV440P8"
    ];

    [Theory]
    [MemberData(nameof(DisplayFormats))]
    public void LoadScript_Format_ConvertsToRgb24(string format)
    {
        SkipIfNativeUnavailable();
        var scriptText = $"""
            import vapoursynth as vs
            core = vs.core
            clip = core.std.BlankClip(width=32, height=16, length=2, format=vs.{format})
            clip.set_output()
            """;

        using var script = VsScript.LoadScript(scriptText);
        using var output = script.GetOutput();
        var info = output.VideoInfo;
        using var frame = output.GetFrame(0);

        Assert.Equal(VsColorFamily.RGB, info.Format.ColorFamily);
        Assert.Equal(8, info.Format.BitsPerSample);
        Assert.Equal(3, info.Format.NumPlanes);
        Assert.Equal(32, info.Width);
        Assert.Equal(16, info.Height);
        Assert.Equal(32, frame.GetPlane(0).Width);
        Assert.Equal(16, frame.GetPlane(0).Height);
        Assert.NotEqual(IntPtr.Zero, frame.GetPlane(0).Ptr);
        Assert.NotEqual(IntPtr.Zero, frame.GetPlane(1).Ptr);
        Assert.NotEqual(IntPtr.Zero, frame.GetPlane(2).Ptr);
        Assert.Equal(format, script.SourceVideoInfo?.Format.Name);
        if (!format.StartsWith("RGB", StringComparison.Ordinal))
        {
            Assert.NotEqual(VsColorFamily.RGB, script.SourceVideoInfo!.Format.ColorFamily);
        }
    }

    [Fact]
    public void LoadScript_Yuv420P10_SourceVideoInfoKeepsTenBit()
    {
        SkipIfNativeUnavailable();
        var scriptText = """
            import vapoursynth as vs
            core = vs.core
            clip = core.std.BlankClip(width=32, height=16, length=2, format=vs.YUV420P10)
            clip.set_output()
            """;

        using var script = VsScript.LoadScript(scriptText);
        using var output = script.GetOutput();
        var source = script.SourceVideoInfo!;

        Assert.Equal(VsColorFamily.RGB, output.VideoInfo.Format.ColorFamily);
        Assert.Equal(8, output.VideoInfo.Format.BitsPerSample);
        Assert.Equal("YUV420P10", source.Format.Name);
        Assert.Equal(VsColorFamily.YUV, source.Format.ColorFamily);
        Assert.Equal(10, source.Format.BitsPerSample);
        Assert.Equal(32, source.Width);
        Assert.Equal(16, source.Height);
        Assert.Equal(2, source.NumFrames);
        Assert.Equal("4:2:0", VsFormatName.Subsampling(source.Format));
    }

    [Fact]
    public void GetSourceFrameProperties_SetFrameProps_ReturnsSourceKeys()
    {
        SkipIfNativeUnavailable();
        var scriptText = """
            import vapoursynth as vs
            core = vs.core
            clip = core.std.BlankClip(width=16, height=16, length=2, format=vs.YUV420P8)
            clip = clip.std.SetFrameProps(_Matrix=1, _PictType="I")
            clip.set_output()
            """;

        using var script = VsScript.LoadScript(scriptText);
        var properties = script.GetSourceFrameProperties(0);
        var map = properties.ToDictionary(x => x.Name, x => x.Value, StringComparer.Ordinal);

        Assert.Equal("1", map["_Matrix"]);
        Assert.Equal("I", map["_PictType"]);
    }

    [Theory]
    [InlineData("YUV420P8")]
    [InlineData("YUV420P10")]
    [InlineData("YUV420P16")]
    [InlineData("YUV420PS")]
    [InlineData("RGB48")]
    [InlineData("RGBS")]
    public void LoadScript_FormatRed_ConvertsToRedRgb24(string format)
    {
        SkipIfNativeUnavailable();
        var scriptText =
            "import vapoursynth as vs\n" +
            "core = vs.core\n" +
            "clip = core.std.BlankClip(width=16, height=16, length=1, format=vs.RGB24, color=[255, 0, 0])\n" +
            "fmt = vs." + format + "\n" +
            "if clip.format.id != fmt:\n" +
            "    args = {\"format\": fmt}\n" +
            "    if \"" + format + "\".startswith((\"YUV\", \"GRAY\")):\n" +
            "        args[\"matrix_s\"] = \"170m\"\n" +
            "    clip = clip.resize.Bicubic(**args)\n" +
            "clip.set_output()\n";

        using var script = VsScript.LoadScript(scriptText);
        AssertRedRgb(script);
    }

    [Fact]
    public void LoadScript_Yuv420Red_ConvertsToRedRgb24()
    {
        SkipIfNativeUnavailable();

        using var script = VsScript.LoadScript("""
            import vapoursynth as vs
            core = vs.core
            clip = core.std.BlankClip(width=16, height=16, length=1, format=vs.YUV420P8, color=[81, 90, 240])
            clip.set_output()
            """);
        AssertRedRgb(script);
    }

    [Fact]
    public void LoadScript_Ffms2Yuv420File_ConvertsToRedRgb24()
    {
        SkipIfNativeUnavailable();
        var path = Path.Combine(Path.GetTempPath(), $"SynthMultiViewer-{Guid.NewGuid():N}.mp4");
        try
        {
            var ffmpeg = Run("ffmpeg", $"-y -f lavfi -i color=c=red:s=32x16:d=1 -pix_fmt yuv420p \"{path}\"");
            if (ffmpeg != 0 || !File.Exists(path))
            {
                Assert.Skip("ffmpeg is not available to create a YUV420 sample.");
            }

            using var script = VsScript.LoadScript($"""
                import vapoursynth as vs
                core = vs.core
                clip = core.ffms2.Source(r"{path}")
                clip.set_output()
                """);
            AssertRedRgb(script);
        }
        catch (VsException ex) when (ex.Message.Contains("ffms2", StringComparison.OrdinalIgnoreCase) ||
                                     ex.Message.Contains("No attribute", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Skip("VapourSynth FFMS2 plugin was not loaded.");
        }
        finally
        {
            foreach (var leftover in new[] { path, path + ".ffindex" })
            {
                if (File.Exists(leftover))
                {
                    File.Delete(leftover);
                }
            }
        }
    }

    [Fact]
    public void LoadScript_Yuv420IdentityMatrix_StillConvertsToRedRgb24()
    {
        SkipIfNativeUnavailable();

        using var script = VsScript.LoadScript("""
            import vapoursynth as vs
            core = vs.core
            clip = core.std.BlankClip(width=16, height=16, length=1, format=vs.YUV420P8, color=[81, 90, 240])
            clip = clip.std.SetFrameProps(_Matrix=0)
            clip.set_output()
            """);
        AssertRedRgb(script);
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
        Assert.Contains("did not set", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("output", error.Message, StringComparison.OrdinalIgnoreCase);
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
    public void TryEvaluate_NativeLibrary_ReturnsTrue()
    {
        SkipIfNativeUnavailable();

        var usable = VsScript.TryEvaluate(out var error);

        Assert.True(usable);
        Assert.Null(error);
    }

    [Fact]
    public void TryReadVersion_NativeLibrary_ReturnsRelease()
    {
        SkipIfNativeUnavailable();

        var read = VsScript.TryReadVersion(out var version, out var detail);

        Assert.True(read);
        Assert.StartsWith("R", version, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(detail));
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
    public void GetFrameAsync_QueuedFrames_ReadyHandlerCanReadFrameProperties()
    {
        SkipIfNativeUnavailable();
        const int count = 5;
        using var done = new ManualResetEventSlim(false);
        var ready = 0;
        IReadOnlyList<(string Name, string Value)>? properties = null;

        using var script = VsScript.LoadScript(YuvBlankClip);
        using var output = script.GetOutput();
        output.FrameReady += (_, e) =>
        {
            properties = e.Frame?.GetProperties();
            if (Interlocked.Increment(ref ready) >= count)
            {
                done.Set();
            }
        };
        for (var i = 0; i < count; i++)
        {
            output.GetFrameAsync(i);
        }

        Assert.True(done.Wait(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));
        Assert.Equal(count, ready);
        Assert.NotNull(properties);
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

    private static void AssertRedRgb(VsScript script)
    {
        using var output = script.GetOutput();
        using var frame = output.GetFrame(0);
        var red = Marshal.ReadByte(frame.GetPlane(0).Ptr);
        var green = Marshal.ReadByte(frame.GetPlane(1).Ptr);
        var blue = Marshal.ReadByte(frame.GetPlane(2).Ptr);

        Assert.Equal(VsColorFamily.RGB, output.VideoInfo.Format.ColorFamily);
        Assert.True(red > 200, $"red={red} green={green} blue={blue}");
        Assert.True(red > green + 80);
        Assert.True(red > blue + 80);
    }

    private static int Run(string fileName, string arguments)
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            });
            if (process == null)
            {
                return -1;
            }

            process.WaitForExit(15000);
            return process.HasExited ? process.ExitCode : -1;
        }
        catch (Exception)
        {
            return -1;
        }
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
