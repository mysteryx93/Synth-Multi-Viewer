using System.Runtime.InteropServices;

namespace HanumanInstitute.ApiAviSynth;

/// <summary>
/// Resolves AviSynth library and plugin locations for the current operating system.
/// Bundled or locally built plugins stay separate from distro/AUR system plugin directories.
/// Foreign bitness or ISA folders are dropped so autoload cannot crash the host.
/// </summary>
public static class AvsPathResolver
{
    private static readonly string? InheritedExtraPluginPath =
        Environment.GetEnvironmentVariable("AVISYNTH_PLUGIN_PATH");

    private static readonly IReadOnlyList<string> UnixLibraryRoots = OperatingSystem.IsMacOS()
        ? ["/opt/homebrew/lib", "/usr/local/lib"]
        : CreateLinuxLibraryRoots();

    /// <summary>
    /// Gets the absolute path used when evaluating a script file.
    /// </summary>
    public static string ResolveScriptPath(string path) => Path.GetFullPath(path.CheckNotNullOrEmpty());

    /// <summary>
    /// Gets native library file names for the current OS.
    /// </summary>
    public static IReadOnlyList<string> LibraryFileNames => OperatingSystem.IsWindows()
        ? ["AviSynth.dll", "avisynth.dll"]
        : OperatingSystem.IsMacOS()
            ? ["libavisynth.dylib"]
            : ["libavisynth.so", "libavisynth.so.3"];

    /// <summary>
    /// Returns candidate paths for the AviSynth library.
    /// </summary>
    /// <param name="overridePath">An explicit file or directory. When set, only that location is searched.</param>
    public static IReadOnlyList<string> GetLibraryCandidates(string? overridePath = null)
    {
        if (overridePath.HasValue())
        {
            return ExpandConfiguredPath(overridePath);
        }

        var candidates = new List<string>();
        candidates.AddRange(ExpandConfiguredPath(Environment.GetEnvironmentVariable("AVISYNTH_PATH")));
        foreach (var directory in GetInstallDirectories())
        {
            foreach (var fileName in LibraryFileNames)
            {
                candidates.Add(Path.Combine(directory, fileName));
            }
        }

        candidates.AddRange(LibraryFileNames);
        return candidates;
    }

    /// <summary>
    /// Returns an absolute path for a library that was loaded by file name.
    /// </summary>
    public static string ResolveExistingPath(string candidate, IntPtr loadedHandle = default)
    {
        candidate.CheckNotNullOrEmpty();
        if (File.Exists(candidate))
        {
            return Path.GetFullPath(candidate);
        }

        var mapped = ResolveFromProcessMaps(candidate, loadedHandle);
        if (mapped != null)
        {
            return mapped;
        }

        foreach (var directory in GetInstallDirectories())
        {
            foreach (var fileName in DistinctFileNames(candidate))
            {
                var full = Path.Combine(directory, fileName);
                if (File.Exists(full))
                {
                    return Path.GetFullPath(full);
                }
            }
        }

        return candidate;
    }

    /// <summary>
    /// Returns directories that commonly contain the AviSynth library.
    /// </summary>
    public static IReadOnlyList<string> GetInstallDirectories()
    {
        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            return
            [
                Path.Combine(programFiles, "AviSynth+"),
                Path.Combine(programFiles, "AviSynth")
            ];
        }

        return UnixLibraryRoots;
    }

    /// <summary>
    /// Returns existing plugin directories, bundled first, then system extras.
    /// </summary>
    public static IReadOnlyList<string> GetPluginDirectories(string? libraryPath) =>
        FilterDirectories(
            GetBundledPluginDirectories(libraryPath)
                .Concat(GetSystemPluginDirectories())
                .Where(Directory.Exists));

    /// <summary>
    /// Returns plugin directories shipped with the loaded library.
    /// </summary>
    public static IReadOnlyList<string> GetBundledPluginDirectories(string? libraryPath)
    {
        if (!libraryPath.HasValue() || !Path.IsPathRooted(libraryPath)) { return []; }

        var libDir = Path.GetDirectoryName(libraryPath);
        if (!libDir.HasValue()) { return []; }

        var parent = Directory.GetParent(libDir)?.FullName;
        if (!parent.HasValue())
        {
            return [Path.Combine(libDir, "plugins+"), Path.Combine(libDir, "plugins")];
        }

        return
        [
            Path.Combine(libDir, "plugins+"), Path.Combine(libDir, "plugins"),
            Path.Combine(parent, "plugins+"), Path.Combine(parent, "plugins")
        ];
    }

    /// <summary>
    /// Returns extra plugin directories from the environment and the OS package layout.
    /// On Linux this includes distro/AUR paths such as <c>/usr/lib/avisynth</c> separately from bundled app plugins.
    /// </summary>
    public static IReadOnlyList<string> GetSystemPluginDirectories()
    {
        var directories = new List<string>();
        if (InheritedExtraPluginPath.HasValue())
        {
            directories.AddRange(InheritedExtraPluginPath.Split(
                Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            directories.AddRange(UnixLibraryRoots.Select(root => Path.Combine(root, "avisynth")));
        }

        if (OperatingSystem.IsLinux())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (home.HasValue())
            {
                directories.Add(Path.Combine(home, ".local", "lib", "avisynth"));
            }
        }
        else if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            directories.Add(Path.Combine(programFiles, "AviSynth+", "plugins+"));
            directories.Add(Path.Combine(programFiles, "AviSynth+", "plugins"));
            directories.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AviSynth", "plugins"));
        }

        return directories;
    }

    internal static IReadOnlyList<string> FilterDirectories(IEnumerable<string> directories) =>
        FilterDirectories(directories, RuntimeInformation.ProcessArchitecture);

    internal static IReadOnlyList<string> FilterDirectories(IEnumerable<string> directories, Architecture arch)
    {
        directories.CheckNotNull();
        arch.CheckEnumValid();
        var list = directories.Where(directory => MatchesDirectory(directory, arch)).Distinct(StringComparer.Ordinal).ToList();
        return !Is64BitArchitecture(arch) ? list : list.Where(directory => !HasLib64Twin(directory, list)).ToList();
    }

    internal static bool MatchesDirectory(string path, Architecture arch)
    {
        path.CheckNotNullOrEmpty();
        arch.CheckEnumValid();
        var normalized = Normalize(path);
        if (IsForeignWindowsFolder(normalized, arch)) { return false; }

        var is64Bit = Is64BitArchitecture(arch);
        return !normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => IsForeignSegment(segment, arch, is64Bit));
    }

    internal static string? LinuxMultiarchDirectory(Architecture arch) => arch switch
    {
        Architecture.X64 => "/usr/lib/x86_64-linux-gnu",
        Architecture.X86 => "/usr/lib/i386-linux-gnu",
        Architecture.Arm64 => "/usr/lib/aarch64-linux-gnu",
        Architecture.Arm or Architecture.Armv6 => "/usr/lib/arm-linux-gnueabihf",
        _ => null
    };

    internal static bool Is64BitArchitecture(Architecture arch) => arch is
        Architecture.X64 or Architecture.Arm64 or Architecture.S390x
        or Architecture.Ppc64le or Architecture.LoongArch64 or Architecture.RiscV64;

    private static IReadOnlyList<string> CreateLinuxLibraryRoots()
    {
        var roots = new List<string> { "/usr/lib" };
        if (Environment.Is64BitProcess)
        {
            roots.Add("/usr/lib64");
        }

        var multiarch = LinuxMultiarchDirectory(RuntimeInformation.ProcessArchitecture);
        if (multiarch != null)
        {
            roots.Add(multiarch);
        }

        roots.Add("/usr/local/lib");
        return roots;
    }

    private static IReadOnlyList<string> DistinctFileNames(string candidate)
    {
        var names = new List<string>();
        var fileName = Path.GetFileName(candidate);
        if (fileName.HasValue())
        {
            names.Add(fileName);
        }

        foreach (var name in LibraryFileNames)
        {
            if (!names.Contains(name, StringComparer.Ordinal))
            {
                names.Add(name);
            }
        }

        return names;
    }

    private static string? ResolveFromProcessMaps(string candidate, IntPtr loadedHandle)
    {
        if (loadedHandle == IntPtr.Zero || !OperatingSystem.IsLinux() || !File.Exists("/proc/self/maps"))
        {
            return null;
        }

        var needles = DistinctFileNames(candidate);
        foreach (var line in File.ReadLines("/proc/self/maps"))
        {
            var start = line.IndexOf('/');
            if (start < 0) { continue; }

            var path = line[start..];
            if (needles.Any(name => path.Contains(name, StringComparison.Ordinal)) && File.Exists(path))
            {
                return Path.GetFullPath(path);
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ExpandConfiguredPath(string? path)
    {
        if (!path.HasValue()) { return []; }
        if (File.Exists(path) || !Directory.Exists(path)) { return [path]; }

        var candidates = new List<string>(LibraryFileNames.Count);
        candidates.AddRange(LibraryFileNames.Select(fileName => Path.Combine(path, fileName)));
        return candidates;
    }

    private static bool IsForeignWindowsFolder(string normalized, Architecture arch) =>
        (normalized.Contains("program files (x86)", StringComparison.Ordinal) && arch != Architecture.X86)
        || (normalized.Contains("program files (arm)", StringComparison.Ordinal)
            && arch is not (Architecture.Arm or Architecture.Armv6));

    private static bool IsForeignSegment(string segment, Architecture arch, bool is64Bit) => segment switch
    {
        "plugins32" or "lib32" or "syswow64" => is64Bit,
        "plugins64" or "lib64" or "libx32" => !is64Bit,
        "x86_64-linux-gnu" => arch != Architecture.X64,
        "i386-linux-gnu" or "i686-linux-gnu" => arch != Architecture.X86,
        "aarch64-linux-gnu" => arch != Architecture.Arm64,
        "arm-linux-gnueabihf" or "arm-linux-gnueabi" => arch is not (Architecture.Arm or Architecture.Armv6),
        _ => false
    };

    private static bool HasLib64Twin(string directory, IReadOnlyList<string> directories)
    {
        var normalized = Normalize(directory);
        if (normalized.Contains("-linux-gnu", StringComparison.Ordinal)) { return false; }

        var index = normalized.IndexOf("/lib/", StringComparison.OrdinalIgnoreCase);
        if (index < 0) { return false; }

        var twin = string.Concat(normalized.AsSpan(0, index), "/lib64/", normalized.AsSpan(index + "/lib/".Length));
        return directories.Any(other => Normalize(other).Equals(twin, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimEnd('/').ToLowerInvariant();
}
