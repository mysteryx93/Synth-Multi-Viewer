using System.Runtime.InteropServices;

namespace HanumanInstitute.ApiVapourSynth;

internal static class NativeScriptHosts
{
    public const string VapourSynth = "vsscript";

    public static NativeLibraryProfile VapourSynthScript { get; } = new()
    {
        Id = VapourSynth,
        ImportName = VapourSynth,
        FileNames = FileNamesForCurrentOs(),
        RequiredExports = ["getVSScriptAPI"],
        PathEnvironmentVariable = "VSSCRIPT_PATH",
        ExtraPluginPathEnvironmentVariable = "VAPOURSYNTH_EXTRA_PLUGIN_PATH"
    };

    private static IReadOnlyList<string> FileNamesForCurrentOs()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ["VSScript.dll", "vsscript.dll"];
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return ["libvsscript.dylib", "libvapoursynth-script.dylib"];
        }

        return ["libvsscript.so", "libvapoursynth-script.so", "libvapoursynth-script.so.0"];
    }
}
