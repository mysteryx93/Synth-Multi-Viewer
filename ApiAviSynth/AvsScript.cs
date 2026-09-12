using System.Runtime.InteropServices;

namespace HanumanInstitute.ApiAviSynth;

/// <summary>
/// Owns an AviSynth script environment and its default video clip.
/// </summary>
public sealed class AvsScript : IDisposable
{
    private readonly AvsNative _native;
    private IntPtr _environment;
    private IntPtr _clip;

    private AvsScript(AvsNative native, IntPtr environment, IntPtr clip)
    {
        _native = native;
        _environment = environment;
        _clip = clip;
    }

    /// <summary>
    /// Gets the absolute path used when evaluating a script file.
    /// </summary>
    public static string ResolveScriptPath(string path) => AvsPathResolver.ResolveScriptPath(path);

    /// <summary>
    /// Sets the AviSynth library file or directory before loading a script.
    /// </summary>
    public static void SetDllPath(string? path) => AvsNative.SetDllPath(path);

    /// <summary>
    /// Sets extra plugin folders. Add mode keeps detected folders; replace uses only these folders.
    /// </summary>
    public static void SetPluginFolders(IEnumerable<string>? folders, bool replace = false) =>
        AvsNative.SetPluginFolders(folders, replace);

    /// <summary>
    /// Returns detected plugin directories for the specified library path.
    /// </summary>
    public static IReadOnlyList<string> GetPluginDirectories(string? libraryPath = null) =>
        AvsPathResolver.GetPluginDirectories(libraryPath);

    /// <summary>
    /// Returns whether an AviSynth library that exports the C API can be loaded.
    /// </summary>
    public static bool TryFindLibrary(out string? path) => AvsNative.TryFindLibrary(out path);

    /// <summary>
    /// Loads an AviSynth script file with its directory as the working directory.
    /// </summary>
    public static AvsScript LoadFile(string path)
    {
        var resolvedPath = ResolveScriptPath(path);
        return Load((native, environment) =>
            native.Eval(environment, "ConvertToRGB32(Import(\"" + Escape(resolvedPath) + "\"))"));
    }

    /// <summary>
    /// Evaluates AviSynth script text and returns its video clip.
    /// </summary>
    public static AvsScript LoadScript(string script)
    {
        script.CheckNotNullOrEmpty();
        return Load((native, environment) => native.Eval(environment, script.TrimEnd() + "\nConvertToRGB32()"));
    }

    private static string Escape(string value) => value.Replace("\"", "\"\"", StringComparison.Ordinal);

    private static AvsScript Load(Func<AvsNative, IntPtr, AvsValue> evaluate)
    {
        var native = AvsNative.Load();
        var environment = native.CreateEnvironment();
        if (environment == IntPtr.Zero)
        {
            throw new AvsException("AviSynth could not create a script environment.");
        }

        try
        {
            native.ApplyPluginFolders(environment);
            var result = evaluate(native, environment);
            if (result.Type == (short)'e')
            {
                throw new AvsException(result.GetString() ?? "AviSynth could not evaluate the script.");
            }

            var clip = native.TakeClip(result, environment);
            native.ReleaseValue(result);
            if (clip == IntPtr.Zero)
            {
                throw new AvsException("The AviSynth script did not return a video clip.");
            }

            return new AvsScript(native, environment, clip);
        }
        catch
        {
            native.DeleteEnvironment(environment);
            throw;
        }
    }

    /// <summary>
    /// Gets the script video information.
    /// </summary>
    public AvsVideoInfo VideoInfo
    {
        get
        {
            ObjectDisposedException.ThrowIf(_clip == IntPtr.Zero, this);
            return _native.GetVideoInfo(_clip);
        }
    }

    /// <summary>
    /// Gets a video frame. The returned frame must be disposed.
    /// </summary>
    public AvsFrame GetFrame(int index)
    {
        ObjectDisposedException.ThrowIf(_clip == IntPtr.Zero, this);
        var frame = _native.GetFrame(_clip, index);
        if (frame == IntPtr.Zero)
        {
            throw new AvsException("AviSynth could not return the requested frame.");
        }

        return new AvsFrame(_native, frame);
    }

    /// <summary>
    /// Gets the native AviSynth clip handle for advanced API use.
    /// </summary>
    public IntPtr Clip
    {
        get
        {
            ObjectDisposedException.ThrowIf(_clip == IntPtr.Zero, this);
            return _clip;
        }
    }

    /// <summary>
    /// Releases the clip and script environment.
    /// </summary>
    public void Dispose()
    {
        if (_environment == IntPtr.Zero) { return; }

        if (_clip != IntPtr.Zero)
        {
            _native.ReleaseClip(_clip);
            _clip = IntPtr.Zero;
        }

        _native.DeleteEnvironment(_environment);
        _environment = IntPtr.Zero;
    }
}
