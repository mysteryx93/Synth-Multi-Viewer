namespace HanumanInstitute.ApiVapourSynth;

/// <summary>
/// Owns a VapourSynth 4 script environment and its outputs.
/// </summary>
public sealed class VsScript : IDisposable
{
    private readonly VsScriptApi _scriptApi;
    private readonly IntPtr _handle;
    private bool _disposed;

    private VsScript(VsScriptApi scriptApi, IntPtr handle)
    {
        _scriptApi = scriptApi;
        _handle = handle;
    }

    /// <summary>
    /// Creates an empty script environment.
    /// </summary>
    public static VsScript CreateEmpty()
    {
        var scriptApi = VsScriptApi.Load();
        var handle = scriptApi.CreateScript();
        if (handle == IntPtr.Zero)
        {
            throw new VsException("VapourSynth could not create a script environment.");
        }

        return new VsScript(scriptApi, handle);
    }

    /// <summary>
    /// Gets the absolute path used when evaluating a script file.
    /// </summary>
    public static string ResolveScriptPath(string path) => VsPathResolver.ResolveScriptPath(path);

    /// <summary>
    /// Loads a script file and evaluates it with its directory as the working directory.
    /// </summary>
    /// <param name="path">The script file to load.</param>
    public static VsScript LoadFile(string path)
    {
        var resolvedPath = ResolveScriptPath(path);
        return LoadScript(File.ReadAllText(resolvedPath), resolvedPath);
    }

    /// <summary>
    /// Loads a script file.
    /// </summary>
    /// <param name="path">The script file to load.</param>
    /// <param name="convertToCompatBgr32">Unsupported by VapourSynth 4. The output must be converted in the script.</param>
    public static VsScript LoadFile(string path, bool convertToCompatBgr32)
    {
        if (convertToCompatBgr32)
        {
            throw new NotSupportedException("COMPATBGR32 was removed in VapourSynth 4. Convert the output to RGB in the script.");
        }

        return LoadFileDirect(path, true);
    }

    /// <summary>
    /// Loads a script file and optionally sets its directory as the temporary working directory.
    /// </summary>
    public static VsScript LoadFileDirect(string path, bool setWorkingDir)
    {
        var resolvedPath = ResolveScriptPath(path);
        var script = CreateEmpty();
        using var fileName = new Utf8Ptr(resolvedPath);

        script._scriptApi.SetWorkingDirectory(script._handle, setWorkingDir);
        if (script._scriptApi.EvaluateFile(script._handle, fileName.ptr) == 0)
        {
            return script;
        }

        var error = script.GetError();
        script.Dispose();
        throw new VsException(error ?? "VapourSynth could not evaluate the script.");
    }

    /// <summary>
    /// Loads script text.
    /// </summary>
    public static VsScript LoadScript(string script) => LoadScript(script, null);

    /// <summary>
    /// Loads script text using an optional source path for error messages, <c>__file__</c>,
    /// and relative imports. VapourSynth evaluates the buffer; the path is not read from disk.
    /// Omit the path for unsaved text so evaluation does not invent a working directory.
    /// </summary>
    public static VsScript LoadScript(string script, string? scriptPath)
    {
        script.CheckNotNullOrEmpty();
        var environment = CreateEmpty();
        try
        {
            environment.EvaluateBuffer(script, scriptPath);
            environment.ConvertOutputToRgb24();
            return environment;
        }
        catch
        {
            environment.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Evaluates a tiny RGB clip to verify the loaded VapourSynth can run scripts.
    /// </summary>
    public static bool TryEvaluate(out string? error)
    {
        try
        {
            using var script = LoadScript(ProbeScript);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or VsException)
        {
            error = ex.Message;
            return false;
        }
    }

    private void EvaluateBuffer(string script, string? scriptPath)
    {
        using var buffer = new Utf8Ptr(script);
        EvaluateNamedBuffer(buffer.ptr, scriptPath, "VapourSynth could not evaluate the script.");
    }

    /// <summary>
    /// Converts output 0 to RGB24 after the user script has finished, so display packing
    /// does not change the graph the script built.
    /// </summary>
    private void ConvertOutputToRgb24()
    {
        using var buffer = new Utf8Ptr(DisplayConversion);
        EvaluateNamedBuffer(buffer.ptr, null, "VapourSynth could not convert the output for display.");
    }

    // VSScript uses "<string>" when the filename is NULL. Some releases still pass that
    // NULL into compile()/os.path and raise "expected str, bytes or os.PathLike object,
    // not NoneType" on unsaved buffers, so always send the placeholder name.
    private const string UnsavedScriptName = "<string>";

    private void EvaluateNamedBuffer(IntPtr buffer, string? scriptPath, string fallbackError)
    {
        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            using var unsaved = new Utf8Ptr(UnsavedScriptName);
            if (_scriptApi.EvaluateBuffer(_handle, buffer, unsaved.ptr) != 0)
            {
                throw new VsException(GetError() ?? fallbackError);
            }

            return;
        }

        var resolvedPath = ResolveScriptPath(scriptPath);
        using var fileName = new Utf8Ptr(resolvedPath);
        var directory = Path.GetDirectoryName(resolvedPath);
        if (directory.HasValue() && Directory.Exists(directory))
        {
            _scriptApi.SetWorkingDirectory(_handle, true);
        }

        if (_scriptApi.EvaluateBuffer(_handle, buffer, fileName.ptr) != 0)
        {
            throw new VsException(GetError() ?? fallbackError);
        }
    }

    private const string ProbeScript = """
        import vapoursynth as vs
        core = vs.core
        clip = core.std.BlankClip(width=16, height=16, length=1, format=vs.RGB24)
        clip.set_output()
        """;

    private const string DisplayConversion = """
import vapoursynth as synthmultiviewer_vs
synthmultiviewer_outputs = synthmultiviewer_vs.get_outputs()
if 0 not in synthmultiviewer_outputs:
    raise synthmultiviewer_vs.Error("The script did not set video output 0.")
synthmultiviewer_out = synthmultiviewer_outputs[0]
synthmultiviewer_node = synthmultiviewer_out.clip if hasattr(synthmultiviewer_out, "clip") else synthmultiviewer_out
if not isinstance(synthmultiviewer_node, synthmultiviewer_vs.VideoNode):
    raise synthmultiviewer_vs.Error("Output 0 is not a video node.")
synthmultiviewer_format = synthmultiviewer_node.format
synthmultiviewer_rgb24 = (
    synthmultiviewer_format is not None
    and synthmultiviewer_format.color_family == synthmultiviewer_vs.RGB
    and synthmultiviewer_format.bits_per_sample == 8
    and synthmultiviewer_format.sample_type == synthmultiviewer_vs.INTEGER
    and synthmultiviewer_format.num_planes == 3
)
if not synthmultiviewer_rgb24:
    synthmultiviewer_args = {"format": synthmultiviewer_vs.RGB24}
    if synthmultiviewer_format is None or synthmultiviewer_format.color_family in (
        synthmultiviewer_vs.YUV, synthmultiviewer_vs.GRAY):
        synthmultiviewer_mid = None
        try:
            synthmultiviewer_mid = synthmultiviewer_node.get_frame(0).props.get("_Matrix")
        except Exception:
            synthmultiviewer_mid = None
        if synthmultiviewer_mid == 1:
            synthmultiviewer_mat = "709"
        elif synthmultiviewer_mid in (5, 6):
            synthmultiviewer_mat = "170m"
        elif synthmultiviewer_mid in (9, 10):
            synthmultiviewer_mat = "2020ncl"
        else:
            synthmultiviewer_mat = "709" if synthmultiviewer_node.height >= 720 else "170m"
        # zimg keeps _Matrix over matrix_in_s when they disagree, so tag must match.
        synthmultiviewer_node = synthmultiviewer_node.std.SetFrameProps(
            _Matrix={"709": 1, "170m": 6, "2020ncl": 9}[synthmultiviewer_mat])
        synthmultiviewer_args["matrix_in_s"] = synthmultiviewer_mat
    synthmultiviewer_node = synthmultiviewer_node.resize.Bicubic(**synthmultiviewer_args)
synthmultiviewer_node.set_output()
""";

    /// <summary>
    /// Returns the first video output.
    /// </summary>
    public VsOutput GetOutput() => GetOutput(0);

    /// <summary>
    /// Returns the video output at the specified index.
    /// </summary>
    public VsOutput GetOutput(int index)
    {
        ThrowIfDisposed();
        return new VsOutput(_scriptApi, _handle, index);
    }

    /// <summary>
    /// Releases the script environment after all outputs and frames have been released.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) { return; }

        _scriptApi.FreeScript(_handle);
        _disposed = true;
    }

    private string? GetError() => _scriptApi.GetError(_handle);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
