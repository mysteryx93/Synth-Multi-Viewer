using System.Reflection;
using System.Runtime.InteropServices;

namespace HanumanInstitute.ApiVapourSynth;

/// <summary>
/// Loads the VapourSynth script library and applies extra plugin directories.
/// Path discovery lives in <see cref="VsPathResolver"/>.
/// </summary>
internal static class NativeLibraryLocator
{
    private static readonly Dictionary<string, string> Overrides = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, IntPtr> Loaded = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> LoadedPaths = new(StringComparer.Ordinal);
    private static IReadOnlyList<string> _extraPluginFolders = [];
    private static bool _replacePluginFolders;

    public static void SetOverride(string libraryId, string path)
    {
        Loaded.Remove(libraryId);
        LoadedPaths.Remove(libraryId);
        if (!path.HasValue())
        {
            Overrides.Remove(libraryId);
            return;
        }

        Overrides[libraryId] = path;
    }

    public static bool Matches(NativeLibraryProfile profile, string libraryName) =>
        string.Equals(libraryName, profile.ImportName, StringComparison.OrdinalIgnoreCase) ||
        profile.FileNames.Any(name => string.Equals(libraryName, name, StringComparison.OrdinalIgnoreCase));

    public static IntPtr Resolve(NativeLibraryProfile profile, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (Loaded.TryGetValue(profile.Id, out var existing) && existing != IntPtr.Zero)
        {
            return existing;
        }

        Overrides.TryGetValue(profile.Id, out var overridePath);
        var skippedIncompatible = new List<string>();
        foreach (var candidate in VsPathResolver.GetLibraryCandidates(overridePath))
        {
            if (!TryLoadCandidate(candidate, assembly, searchPath, out var handle, out var loadedPath))
            {
                continue;
            }

            if (!HasRequiredExports(handle, profile.RequiredExports))
            {
                skippedIncompatible.Add(loadedPath);
                NativeLibrary.Free(handle);
                continue;
            }

            Loaded[profile.Id] = handle;
            LoadedPaths[profile.Id] = VsPathResolver.ResolveExistingPath(loadedPath, handle);
            ApplyExtraPluginPath(profile);
            return handle;
        }

        if (skippedIncompatible.Count > 0)
        {
            throw new EntryPointNotFoundException(
                $"Found VapourSynth at '{skippedIncompatible[0]}' but it does not export {string.Join(", ", profile.RequiredExports)}.");
        }

        if (overridePath.HasValue())
        {
            throw new DllNotFoundException($"Could not load VapourSynth from '{overridePath}'.");
        }

        throw new DllNotFoundException(
            "Could not load VapourSynth. Set VSSCRIPT_PATH to its library file or directory.");
    }

    public static bool TryFindLibrary(out string? path)
    {
        try
        {
            Resolve(NativeScriptHosts.VapourSynthScript, typeof(VsHelper).Assembly, null);
            return LoadedPaths.TryGetValue(NativeScriptHosts.VapourSynth, out path) && path.HasValue();
        }
        catch (DllNotFoundException)
        {
            path = null;
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            path = null;
            return false;
        }
    }

    public static IReadOnlyList<string> GetPluginDirectories(NativeLibraryProfile profile)
    {
        LoadedPaths.TryGetValue(profile.Id, out var libraryPath);
        return VsPathResolver.GetPluginDirectories(libraryPath);
    }

    public static void SetPluginFolders(IEnumerable<string>? folders, bool replace)
    {
        _extraPluginFolders = (folders ?? [])
            .Where(dir => dir.HasValue())
            .Select(dir => dir.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        _replacePluginFolders = replace;
        ApplyExtraPluginPath(NativeScriptHosts.VapourSynthScript);
    }

    private static bool TryLoadCandidate(
        string candidate, Assembly assembly, DllImportSearchPath? searchPath, out IntPtr handle, out string loadedPath)
    {
        loadedPath = candidate;
        if (Path.IsPathRooted(candidate))
        {
            return NativeLibrary.TryLoad(candidate, out handle);
        }

        return NativeLibrary.TryLoad(candidate, assembly, searchPath, out handle);
    }

    private static bool HasRequiredExports(IntPtr handle, IReadOnlyList<string> exports) =>
        exports.All(name => NativeLibrary.TryGetExport(handle, name, out _));

    private static void ApplyExtraPluginPath(NativeLibraryProfile profile)
    {
        if (!profile.ExtraPluginPathEnvironmentVariable.HasValue()) { return; }

        LoadedPaths.TryGetValue(profile.Id, out var libraryPath);
        var extra = VsPathResolver.GetExtraPluginDirectories(libraryPath, _extraPluginFolders, _replacePluginFolders);
        Environment.SetEnvironmentVariable(
            profile.ExtraPluginPathEnvironmentVariable,
            string.Join(Path.PathSeparator.ToString(), extra));
    }
}
