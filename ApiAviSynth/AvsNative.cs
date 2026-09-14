using System.Runtime.InteropServices;
using System.Text;

namespace HanumanInstitute.ApiAviSynth;

internal sealed class AvsNative
{
    private const int InterfaceVersion = 6;
    private readonly IntPtr _library;
    private readonly CreateEnvironmentDelegate _createEnvironment;
    private readonly DeleteEnvironmentDelegate _deleteEnvironment;
    private readonly InvokeDelegate _invoke;
    private readonly TakeClipDelegate _takeClip;
    private readonly ReleaseClipDelegate _releaseClip;
    private readonly ReleaseFrameDelegate _releaseFrame;
    private readonly ReleaseValueDelegate _releaseValue;
    private readonly GetVideoInfoDelegate _getVideoInfo;
    private readonly GetFrameDelegate _getFrame;
    private readonly GetPlaneValueDelegate _getPitch;
    private readonly GetPlaneValueDelegate _getRowSize;
    private readonly GetPlaneValueDelegate _getHeight;
    private readonly GetReadPtrDelegate _getReadPtr;
    private readonly SetVarDelegate _setVar;

    private AvsNative(IntPtr library)
    {
        _library = library;
        _createEnvironment = GetDelegate<CreateEnvironmentDelegate>("avs_create_script_environment");
        _deleteEnvironment = GetDelegate<DeleteEnvironmentDelegate>("avs_delete_script_environment");
        _invoke = GetDelegate<InvokeDelegate>("avs_invoke");
        _takeClip = GetDelegate<TakeClipDelegate>("avs_take_clip");
        _releaseClip = GetDelegate<ReleaseClipDelegate>("avs_release_clip");
        _releaseFrame = GetDelegate<ReleaseFrameDelegate>("avs_release_video_frame");
        _releaseValue = GetDelegate<ReleaseValueDelegate>("avs_release_value");
        _getVideoInfo = GetDelegate<GetVideoInfoDelegate>("avs_get_video_info");
        _getFrame = GetDelegate<GetFrameDelegate>("avs_get_frame");
        _getPitch = GetDelegate<GetPlaneValueDelegate>("avs_get_pitch_p");
        _getRowSize = GetDelegate<GetPlaneValueDelegate>("avs_get_row_size_p");
        _getHeight = GetDelegate<GetPlaneValueDelegate>("avs_get_height_p");
        _getReadPtr = GetDelegate<GetReadPtrDelegate>("avs_get_read_ptr_p");
        _setVar = GetDelegate<SetVarDelegate>("avs_set_var");
    }

    public IntPtr CreateEnvironment() => _createEnvironment(InterfaceVersion);
    public void DeleteEnvironment(IntPtr environment) => _deleteEnvironment(environment);
    public IntPtr TakeClip(AvsValue value, IntPtr environment) => _takeClip(value, environment);
    public void ReleaseClip(IntPtr clip) => _releaseClip(clip);
    public void ReleaseFrame(IntPtr frame) => _releaseFrame(frame);
    public void ReleaseValue(AvsValue value) => _releaseValue(value);
    public AvsVideoInfo GetVideoInfo(IntPtr clip) => Marshal.PtrToStructure<AvsVideoInfo>(_getVideoInfo(clip));
    public IntPtr GetFrame(IntPtr clip, int index) => _getFrame(clip, index);
    public int GetPitch(IntPtr frame, int plane) => _getPitch(frame, plane);
    public int GetRowSize(IntPtr frame, int plane) => _getRowSize(frame, plane);
    public int GetHeight(IntPtr frame, int plane) => _getHeight(frame, plane);
    public IntPtr GetReadPtr(IntPtr frame, int plane) => _getReadPtr(frame, plane);

    public AvsValue Import(IntPtr environment, string path) => InvokeString(environment, "Import", path);

    public AvsValue Eval(IntPtr environment, string script) => InvokeString(environment, "Eval", script);

    public void SetVar(IntPtr environment, string name, AvsValue value) => _setVar(environment, name, value);

    public AvsValue InvokeClip(IntPtr environment, string name, IntPtr clip)
    {
        var arg = new AvsValue { Type = (short)'c', Value = clip };
        var argHandle = Marshal.AllocCoTaskMem(Marshal.SizeOf<AvsValue>());
        try
        {
            Marshal.StructureToPtr(arg, argHandle, false);
            return _invoke(environment, name, new AvsValue { Type = (short)'a', ArraySize = 1, Value = argHandle },
                IntPtr.Zero);
        }
        finally
        {
            Marshal.FreeCoTaskMem(argHandle);
        }
    }

    private AvsValue InvokeString(IntPtr environment, string name, string value)
    {
        var bytes = Encoding.Default.GetBytes(value + '\0');
        var memory = Marshal.AllocCoTaskMem(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, memory, bytes.Length);
            return _invoke(environment, name, new AvsValue { Type = (short)'s', Value = memory }, IntPtr.Zero);
        }
        finally
        {
            Marshal.FreeCoTaskMem(memory);
        }
    }

    private static string? OverridePath;
    private static IReadOnlyList<string> ExtraPluginFolders = [];
    private static bool ReplacePluginFolders;

    public static void SetDllPath(string? path) => OverridePath = path.HasValue() ? path : null;

    public static void SetPluginFolders(IEnumerable<string>? folders, bool replace)
    {
        ExtraPluginFolders = (folders ?? [])
            .Where(dir => dir.HasValue())
            .Select(dir => dir.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        ReplacePluginFolders = replace;
    }

    public void ApplyPluginFolders(IntPtr environment)
    {
        if (ReplacePluginFolders)
        {
            TryEval(environment, "ClearAutoloadDirs()");
        }

        foreach (var dir in ExtraPluginFolders)
        {
            TryEval(environment, "AddAutoloadDir(\"" + dir.Replace("\"", "\"\"", StringComparison.Ordinal) + "\")");
        }
    }

    private void TryEval(IntPtr environment, string script)
    {
        var result = Eval(environment, script);
        ReleaseValue(result);
    }

    public static bool TryFindLibrary(out string? path)
    {
        foreach (var candidate in AvsPathResolver.GetLibraryCandidates(OverridePath))
        {
            if (NativeLibrary.TryLoad(candidate, out var library))
            {
                path = AvsPathResolver.ResolveExistingPath(candidate, library);
                NativeLibrary.Free(library);
                return true;
            }
        }

        path = null;
        return false;
    }

    public static AvsNative Load()
    {
        foreach (var candidate in AvsPathResolver.GetLibraryCandidates(OverridePath))
        {
            if (NativeLibrary.TryLoad(candidate, out var library))
            {
                return new AvsNative(library);
            }
        }

        if (OverridePath.HasValue())
        {
            throw new DllNotFoundException($"Could not load AviSynth from '{OverridePath}'.");
        }

        throw new DllNotFoundException(
            "Could not load AviSynth. Set AVISYNTH_PATH to its library file or directory.");
    }

    private T GetDelegate<T>(string export) where T : Delegate
    {
        if (!NativeLibrary.TryGetExport(_library, export, out var pointer))
        {
            throw new EntryPointNotFoundException($"The loaded AviSynth library does not export {export}.");
        }

        return Marshal.GetDelegateForFunctionPointer<T>(pointer);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr CreateEnvironmentDelegate(int version);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void DeleteEnvironmentDelegate(IntPtr environment);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate AvsValue InvokeDelegate(
        IntPtr environment, string name, AvsValue args, IntPtr argNames);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr TakeClipDelegate(
        AvsValue value, IntPtr environment);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void ReleaseClipDelegate(IntPtr clip);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void ReleaseFrameDelegate(IntPtr frame);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void ReleaseValueDelegate(AvsValue value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr GetVideoInfoDelegate(IntPtr clip);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr GetFrameDelegate(IntPtr clip, int index);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int GetPlaneValueDelegate(IntPtr frame, int plane);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr GetReadPtrDelegate(IntPtr frame, int plane);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int SetVarDelegate(IntPtr environment, string name, AvsValue value);
}
