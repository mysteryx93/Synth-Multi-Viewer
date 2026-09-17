using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace HanumanInstitute.ApiVapourSynth;

/// <summary>
/// Resolves VapourSynth library and plugin locations for the current operating system.
/// Bundled plugins (pip, site-packages, portable layouts) stay separate from system plugins
/// (package managers, AUR, <c>/usr/lib/vapoursynth</c>).
/// Foreign bitness or ISA folders are dropped so autoload cannot crash the host.
/// </summary>
public static class VsPathResolver
{
    private static readonly string? InheritedExtraPluginPath =
        Environment.GetEnvironmentVariable(NativeScriptHosts.VapourSynthScript.ExtraPluginPathEnvironmentVariable);

    private static readonly IReadOnlyList<string> UnixLibraryRoots = OperatingSystem.IsMacOS()
        ? ["/opt/homebrew/lib", "/usr/local/lib"]
        : CreateLinuxLibraryRoots();

    /// <summary>
    /// Gets the absolute path used when evaluating a script file.
    /// </summary>
    public static string ResolveScriptPath(string path) => Path.GetFullPath(path.CheckNotNullOrEmpty());

    /// <summary>
    /// Gets native script-library file names for the current OS.
    /// </summary>
    public static IReadOnlyList<string> LibraryFileNames => NativeScriptHosts.VapourSynthScript.FileNames;

    /// <summary>
    /// Returns candidate paths for the VapourSynth script library.
    /// </summary>
    /// <param name="overridePath">An explicit file or directory. When set, only that location is searched.</param>
    public static IReadOnlyList<string> GetLibraryCandidates(string? overridePath = null)
    {
        if (overridePath.HasValue())
        {
            return ExpandConfiguredPath(overridePath, LibraryFileNames);
        }

        var candidates = new List<string>();
        var envPath = Environment.GetEnvironmentVariable(NativeScriptHosts.VapourSynthScript.PathEnvironmentVariable);
        if (envPath.HasValue())
        {
            candidates.AddRange(ExpandConfiguredPath(envPath, LibraryFileNames));
        }

        foreach (var directory in GetInstallDirectories())
        {
            candidates.AddRange(LibraryFileNames.Select(fileName => Path.Combine(directory, fileName)));
        }

        candidates.AddRange(LibraryFileNames);
        return candidates;
    }

    /// <summary>
    /// Returns an absolute path for a library that was loaded by file name.
    /// </summary>
    public static string ResolveExistingPath(string candidate, IntPtr loadedHandle = 0)
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
    /// Returns directories that commonly contain the VapourSynth script library.
    /// </summary>
    public static IReadOnlyList<string> GetInstallDirectories()
    {
        if (!OperatingSystem.IsWindows()) { return UnixLibraryRoots; }
        
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var directories = new List<string>
        {
            Path.Combine(programFiles, "VapourSynth", "core"),
            Path.Combine(programFiles, "VapourSynth")
        };
        directories.AddRange(ReadWindowsRegistryDirectories());
        return directories;

    }

    /// <summary>
    /// Returns existing plugin directories, bundled first, then system extras.
    /// </summary>
    public static IReadOnlyList<string> GetPluginDirectories(string? libraryPath) =>
        FilterDirectories(GetBundledPluginDirectories(libraryPath)
                .Concat(GetSystemPluginDirectories()).Where(Directory.Exists));

    /// <summary>
    /// Returns directories that contain Python script plugins such as havsfunc, not native <c>.so</c> plugins.
    /// </summary>
    public static IReadOnlyList<string> GetPythonModuleDirectories(string? libraryPath = null)
    {
        var directories = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (libraryPath.HasText())
        {
            var directory = File.Exists(libraryPath) || Path.GetExtension(libraryPath).HasValue()
                ? Path.GetDirectoryName(libraryPath)
                : libraryPath;
            for (var i = 0; i < 6 && directory.HasText(); i++)
            {
                AddPythonSites(directory, directories, seen);
                directory = Path.GetDirectoryName(directory);
            }
        }

        foreach (var root in UnixLibraryRoots)
        {
            AddPythonSites(root, directories, seen);
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (home.HasText())
            {
                AddPythonSites(Path.Combine(home, ".local", "lib"), directories, seen);
            }
        }
        else if (OperatingSystem.IsWindows())
        {
            AddPythonSites(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python"),
                directories, seen);
            AddPythonSites(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VapourSynth"), directories, seen);
            AddPythonSites(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Python"), directories, seen);
        }

        return directories;
    }

    /// <summary>
    /// Returns extra plugin directories to inject into VapourSynth.
    /// Add mode keeps detected system folders; replace mode uses only <paramref name="extraDirectories"/>.
    /// </summary>
    public static IReadOnlyList<string> GetExtraPluginDirectories(string? libraryPath, IEnumerable<string>? extraDirectories, bool replace)
    {
        var extras = NormalizeDirectories(extraDirectories);
        if (replace)
        {
            return extras;
        }

        var bundled = GetBundledPluginDirectories(libraryPath).ToHashSet(StringComparer.Ordinal);
        return FilterDirectories(
                GetSystemPluginDirectories().Where(Directory.Exists).Where(dir => !bundled.Contains(dir)))
            .Concat(extras)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Returns plugin directories shipped with the loaded library (pip, site-packages, portable installs).
    /// </summary>
    public static IReadOnlyList<string> GetBundledPluginDirectories(string? libraryPath)
    {
        if (!libraryPath.HasValue() || !Path.IsPathRooted(libraryPath)) { return []; }

        var libDir = Path.GetDirectoryName(libraryPath);
        if (!libDir.HasValue()) { return []; }

        var parent = Directory.GetParent(libDir)?.FullName;
        if (!parent.HasValue())
        {
            return
            [
                Path.Combine(libDir, "vapoursynth", "plugins"),
                Path.Combine(libDir, "plugins"),
                Path.Combine(libDir, "core", "plugins")
            ];
        }

        return
        [
            Path.Combine(libDir, "vapoursynth", "plugins"),
            Path.Combine(libDir, "plugins"),
            Path.Combine(libDir, "core", "plugins"),
            Path.Combine(parent, "vapoursynth", "plugins"),
            Path.Combine(parent, "plugins")
        ];
    }

    /// <summary>
    /// Returns extra plugin directories from the environment, vapoursynth.conf, and the OS package layout.
    /// On Linux this includes distro/AUR paths such as <c>/usr/lib/vapoursynth</c> separately from bundled app plugins.
    /// </summary>
    public static IReadOnlyList<string> GetSystemPluginDirectories()
    {
        var directories = new List<string>();
        if (InheritedExtraPluginPath.HasValue())
        {
            directories.AddRange(InheritedExtraPluginPath.Split(
                Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        directories.AddRange(ReadConfiguredPluginDirectories());
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            directories.AddRange(UnixLibraryRoots.Select(root => Path.Combine(root, "vapoursynth")));
        }

        if (OperatingSystem.IsLinux())
        {
            var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (dataHome.HasValue())
            {
                directories.Add(Path.Combine(dataHome, "vapoursynth", "plugins"));
            }

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (home.HasValue())
            {
                directories.Add(Path.Combine(home, ".local", "lib", "vapoursynth"));
                directories.Add(Path.Combine(home, ".local", "share", "vapoursynth", "plugins"));
            }
        }
        else if (OperatingSystem.IsWindows())
        {
            directories.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VapourSynth",
                Environment.Is64BitProcess ? "plugins64" : "plugins32"));
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

    internal static IReadOnlyList<string> ExpandConfiguredPath(string path, IReadOnlyList<string> fileNames)
    {
        if (File.Exists(path) || !Directory.Exists(path)) { return [path]; }

        var candidates = new List<string>(fileNames.Count);
        candidates.AddRange(fileNames.Select(fileName => Path.Combine(path, fileName)));
        return candidates;
    }

    private static void AddPythonSites(string? root, List<string> directories, HashSet<string> seen)
    {
        if (!root.HasText() || !Directory.Exists(root))
        {
            return;
        }

        var name = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (name is "site-packages" or "dist-packages")
        {
            AddExisting(root, directories, seen);
        }

        AddExisting(Path.Combine(root, "site-packages"), directories, seen);
        AddExisting(Path.Combine(root, "dist-packages"), directories, seen);
        AddExisting(Path.Combine(root, "Lib", "site-packages"), directories, seen);
        try
        {
            foreach (var python in Directory.EnumerateDirectories(root, "python3.*"))
            {
                AddExisting(Path.Combine(python, "site-packages"), directories, seen);
                AddExisting(Path.Combine(python, "dist-packages"), directories, seen);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void AddExisting(string path, List<string> directories, HashSet<string> seen)
    {
        if (!Directory.Exists(path) || !seen.Add(path))
        {
            return;
        }

        directories.Add(path);
    }

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

    private static IReadOnlyList<string> NormalizeDirectories(IEnumerable<string>? directories) =>
        (directories ?? []).Where(dir => dir.HasValue()).Select(dir => dir.Trim())
        .Distinct(StringComparer.Ordinal).ToList();

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

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<string> ReadWindowsRegistryDirectories()
    {
        if (!OperatingSystem.IsWindows()) { return []; }

        string[] hives = ["HKEY_CURRENT_USER", "HKEY_LOCAL_MACHINE"];
        var directories = new List<string>();
        foreach (var hive in hives)
        {
            var dll = GetRegistryString(hive, @"SOFTWARE\VapourSynth", "VSScriptDLL");
            if (dll.HasValue())
            {
                var dir = Path.GetDirectoryName(dll);
                if (dir.HasValue())
                {
                    directories.Add(dir);
                }
            }

            var root = GetRegistryString(hive, @"SOFTWARE\VapourSynth", "Path");
            if (root.HasValue())
            {
                directories.Add(Path.Combine(root, "core"));
                directories.Add(root);
            }
        }
        return directories;
    }

    [SupportedOSPlatform("windows")]
    private static string GetRegistryString(string hive, string key, string value)
    {
        try
        {
            return Microsoft.Win32.Registry.GetValue($@"{hive}\{key}", value, null) as string ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static IReadOnlyList<string> ReadConfiguredPluginDirectories()
    {
        var confPath = Environment.GetEnvironmentVariable("VAPOURSYNTH_CONF_PATH");
        if (!confPath.HasValue())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (OperatingSystem.IsMacOS())
            {
                confPath = Path.Combine(home, "Library", "Application Support", "VapourSynth", "vapoursynth.conf");
            }
            else if (OperatingSystem.IsLinux())
            {
                var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                confPath = configHome.HasValue()
                    ? Path.Combine(configHome, "vapoursynth", "vapoursynth.conf")
                    : Path.Combine(home, ".config", "vapoursynth", "vapoursynth.conf");
            }
        }

        if (!confPath.HasValue() || !File.Exists(confPath)) { return []; }

        var directories = new List<string>();
        foreach (var line in File.ReadLines(confPath))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || !trimmed.Contains('='))
            {
                continue;
            }

            var parts = trimmed.Split('=', 2);
            var key = parts[0].Trim();
            var value = parts[1].Trim();
            if (key is "UserPluginDir" or "SystemPluginDir" && value.HasValue())
            {
                directories.Add(value);
            }
        }
        return directories;
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
