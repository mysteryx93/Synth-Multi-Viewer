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
    /// Loads script text using an optional source path for error messages and relative imports.
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

    private void EvaluateBuffer(string script, string? scriptPath)
    {
        var resolvedPath = string.IsNullOrWhiteSpace(scriptPath) ? null : ResolveScriptPath(scriptPath);
        using var buffer = new Utf8Ptr(script);
        using var fileName = resolvedPath is null ? null : new Utf8Ptr(resolvedPath);

        if (resolvedPath != null)
        {
            _scriptApi.SetWorkingDirectory(_handle, true);
        }

        if (_scriptApi.EvaluateBuffer(_handle, buffer.ptr, fileName?.ptr ?? IntPtr.Zero) != 0)
        {
            throw new VsException(GetError() ?? "VapourSynth could not evaluate the script.");
        }
    }

    /// <summary>
    /// Converts output 0 to RGB24 after the user script has finished, so display packing
    /// does not change the graph the script built.
    /// </summary>
    private void ConvertOutputToRgb24()
    {
        using var buffer = new Utf8Ptr(DisplayConversion);
        if (_scriptApi.EvaluateBuffer(_handle, buffer.ptr, IntPtr.Zero) != 0)
        {
            throw new VsException(GetError() ?? "VapourSynth could not convert the output for display.");
        }
    }

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
        synthmultiviewer_args["matrix_in_s"] = "709" if synthmultiviewer_node.height > 480 else "170m"
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
