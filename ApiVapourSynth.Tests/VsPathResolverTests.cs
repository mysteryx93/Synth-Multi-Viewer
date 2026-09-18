using System.Runtime.InteropServices;
using Xunit;

namespace HanumanInstitute.ApiVapourSynth.Tests;

public class VsPathResolverTests
{
    [Fact]
    public void ResolveScriptPath_RelativePath_ReturnsAbsolutePath()
    {
        var relativePath = Path.Combine("scripts", "example.vpy");

        var resolvedPath = VsPathResolver.ResolveScriptPath(relativePath);

        Assert.Equal(Path.GetFullPath(relativePath), resolvedPath);
    }

    [Fact]
    public void ResolveScriptPath_AbsolutePath_PreservesAbsolutePath()
    {
        var absolutePath = Path.GetFullPath(Path.Combine("scripts", "example.vpy"));

        var resolvedPath = VsPathResolver.ResolveScriptPath(absolutePath);

        Assert.Equal(absolutePath, resolvedPath);
    }

    [Fact]
    public void ResolveScriptPath_EmptyPath_ThrowsArgumentException()
    {
        const string path = "";

        var act = () => VsPathResolver.ResolveScriptPath(path);

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void GetBundledPluginDirectories_RootedLibrary_ReturnsSiblingPluginFolders()
    {
        var libraryPath = Path.Combine(Path.GetTempPath(), "vapoursynth", "libvsscript.so");
        var libDir = Path.GetDirectoryName(libraryPath)!;
        var parent = Directory.GetParent(libDir)!.FullName;

        var directories = VsPathResolver.GetBundledPluginDirectories(libraryPath);

        Assert.Equal(
            [
                Path.Combine(libDir, "vapoursynth", "plugins"),
                Path.Combine(libDir, "plugins"),
                Path.Combine(libDir, "core", "plugins"),
                Path.Combine(parent, "vapoursynth", "plugins"),
                Path.Combine(parent, "plugins")
            ],
            directories);
    }

    [Fact]
    public void GetBundledPluginDirectories_RelativeLibrary_ReturnsEmpty()
    {
        const string libraryPath = "libvsscript.so";

        var directories = VsPathResolver.GetBundledPluginDirectories(libraryPath);

        Assert.Empty(directories);
    }

    [Fact]
    public void GetPythonModuleDirectories_AncestorTree_FindsSitePackages()
    {
        var root = Path.Combine(Path.GetTempPath(), "vs-python-" + Guid.NewGuid().ToString("N"));
        var site = Path.Combine(root, "lib", "python3.14", "site-packages");
        var package = Path.Combine(site, "vapoursynth");
        Directory.CreateDirectory(package);
        var libraryPath = Path.Combine(package, "libvapoursynth.so");
        try
        {
            var directories = VsPathResolver.GetPythonModuleDirectories(libraryPath);

            Assert.Contains(site, directories);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void GetPythonModuleDirectories_LibraryBesidePythonTree_FindsSitePackages()
    {
        var root = Path.Combine(Path.GetTempPath(), "vs-lib-" + Guid.NewGuid().ToString("N"));
        var lib = Path.Combine(root, "lib");
        var site = Path.Combine(lib, "python3.12", "site-packages");
        Directory.CreateDirectory(site);
        var libraryPath = Path.Combine(lib, "libvapoursynth-script.so");
        try
        {
            var directories = VsPathResolver.GetPythonModuleDirectories(libraryPath);

            Assert.Contains(site, directories);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void GetSystemPluginDirectories_Linux_IncludesDistroAndLocalPaths()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux plugin paths");

        var directories = VsPathResolver.GetSystemPluginDirectories();

        Assert.Contains("/usr/lib/vapoursynth", directories);
        Assert.Contains("/usr/local/lib/vapoursynth", directories);
        Assert.DoesNotContain(directories, path => path.Contains("site-packages", StringComparison.Ordinal));
    }

    [Fact]
    public void ResolveExistingPath_RelativeLibraryName_ReturnsAbsoluteWhenInstalled()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux library paths");
        var relative = VsPathResolver.LibraryFileNames[0];
        var installed = VsPathResolver.GetInstallDirectories()
            .SelectMany(directory => VsPathResolver.LibraryFileNames.Select(name => Path.Combine(directory, name)))
            .FirstOrDefault(File.Exists);
        Assert.SkipWhen(installed == null, "VapourSynth is not installed in a known directory");

        var resolved = VsPathResolver.ResolveExistingPath(relative);

        Assert.True(Path.IsPathRooted(resolved));
        Assert.True(File.Exists(resolved));
    }

    [Fact]
    public void ResolveExistingPath_EmptyPath_ThrowsArgumentException()
    {
        const string path = "";

        var act = () => VsPathResolver.ResolveExistingPath(path);

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void GetLibraryCandidates_OverrideFile_ReturnsOnlyThatFile()
    {
        var overridePath = Path.GetTempFileName();
        try
        {
            var candidates = VsPathResolver.GetLibraryCandidates(overridePath);

            Assert.Equal([overridePath], candidates);
        }
        finally
        {
            File.Delete(overridePath);
        }
    }

    [Fact]
    public void ExpandConfiguredPath_Directory_YieldsLibraryFileNames()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var candidates = VsPathResolver.ExpandConfiguredPath(directory.FullName, VsPathResolver.LibraryFileNames);

            Assert.Equal(VsPathResolver.LibraryFileNames.Select(name => Path.Combine(directory.FullName, name)), candidates);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void GetExtraPluginDirectories_Replace_ReturnsOnlyExtras()
    {
        var extras = new[] { "/custom/plugins", "/custom/plugins2" };

        var directories = VsPathResolver.GetExtraPluginDirectories("/usr/lib/libvsscript.so", extras, replace: true);

        Assert.Equal(extras, directories);
    }

    [Fact]
    public void GetExtraPluginDirectories_Add_AppendsExtrasToSystemFolders()
    {
        var extra = Path.Combine(Path.GetTempPath(), "vs-extra-plugins-" + Guid.NewGuid().ToString("N"));

        var directories = VsPathResolver.GetExtraPluginDirectories(null, [extra], replace: false);

        Assert.Contains(extra, directories);
        Assert.DoesNotContain("/custom/not-auto", directories);
    }

    [Fact]
    public void LibraryFileNames_CurrentOs_ContainsSharedLibrary()
    {
        var names = VsPathResolver.LibraryFileNames;

        Assert.Contains(names, name => name.Contains("vsscript", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(Architecture.Arm64, "/usr/lib/aarch64-linux-gnu/vapoursynth", true)]
    [InlineData(Architecture.Arm64, "/usr/lib64/vapoursynth", true)]
    [InlineData(Architecture.Arm64, "/usr/lib/vapoursynth", true)]
    [InlineData(Architecture.Arm64, "/home/u/.config/VapourSynth/plugins64", true)]
    [InlineData(Architecture.Arm64, "/usr/lib/x86_64-linux-gnu/vapoursynth", false)]
    [InlineData(Architecture.Arm64, "/usr/lib/i386-linux-gnu/vapoursynth", false)]
    [InlineData(Architecture.Arm64, "/usr/lib/arm-linux-gnueabihf/vapoursynth", false)]
    [InlineData(Architecture.Arm64, "/home/u/.config/VapourSynth/plugins32", false)]
    [InlineData(Architecture.Arm64, @"C:\Program Files (x86)\VapourSynth\plugins", false)]
    [InlineData(Architecture.Arm64, @"C:\Program Files (Arm)\VapourSynth\plugins", false)]
    [InlineData(Architecture.Arm, "/usr/lib/arm-linux-gnueabihf/vapoursynth", true)]
    [InlineData(Architecture.Arm, "/home/u/.config/VapourSynth/plugins32", true)]
    [InlineData(Architecture.Arm, "/usr/lib/aarch64-linux-gnu/vapoursynth", false)]
    [InlineData(Architecture.Arm, "/usr/lib64/vapoursynth", false)]
    [InlineData(Architecture.Arm, "/home/u/.config/VapourSynth/plugins64", false)]
    [InlineData(Architecture.Armv6, "/usr/lib/arm-linux-gnueabihf/vapoursynth", true)]
    [InlineData(Architecture.X64, "/usr/lib/aarch64-linux-gnu/vapoursynth", false)]
    [InlineData(Architecture.X86, "/usr/lib64/vapoursynth", false)]
    [InlineData(Architecture.X86, "/home/u/.config/VapourSynth/plugins32", true)]
    public void MatchesDirectory_Architecture_KeepsOnlyNativePaths(Architecture arch, string path, bool expected)
    {
        var matches = VsPathResolver.MatchesDirectory(path, arch);

        Assert.Equal(expected, matches);
    }

    [Fact]
    public void FilterDirectories_Arm64_DropsPlainLibWhenLib64TwinExists()
    {
        var input =
            new[]
            {
                "/usr/lib/vapoursynth",
                "/usr/lib64/vapoursynth",
                "/usr/lib/aarch64-linux-gnu/vapoursynth",
                "/usr/lib/x86_64-linux-gnu/vapoursynth"
            };

        var directories = VsPathResolver.FilterDirectories(input, Architecture.Arm64);

        Assert.Equal(
            ["/usr/lib64/vapoursynth", "/usr/lib/aarch64-linux-gnu/vapoursynth"],
            directories);
    }

    [Fact]
    public void FilterDirectories_64Bit_DropsForeignArchitectureAndLibTwin()
    {
        Assert.SkipUnless(Environment.Is64BitProcess, "64-bit process");
        var input = new[]
        {
            "/usr/lib/vapoursynth",
            "/usr/lib64/vapoursynth",
            "/usr/lib/x86_64-linux-gnu/vapoursynth",
            "/usr/lib/aarch64-linux-gnu/vapoursynth",
            "/usr/lib/i386-linux-gnu/vapoursynth",
            "/home/u/.config/VapourSynth/plugins32",
            "/home/u/.config/VapourSynth/plugins64"
        };

        var directories = VsPathResolver.FilterDirectories(input);

        Assert.DoesNotContain("/usr/lib/vapoursynth", directories);
        Assert.Contains("/usr/lib64/vapoursynth", directories);
        var arch = RuntimeInformation.ProcessArchitecture;
        if (arch == Architecture.X64)
        {
            Assert.Contains("/usr/lib/x86_64-linux-gnu/vapoursynth", directories);
            Assert.DoesNotContain("/usr/lib/aarch64-linux-gnu/vapoursynth", directories);
        }
        else if (arch == Architecture.Arm64)
        {
            Assert.Contains("/usr/lib/aarch64-linux-gnu/vapoursynth", directories);
            Assert.DoesNotContain("/usr/lib/x86_64-linux-gnu/vapoursynth", directories);
        }
        Assert.DoesNotContain("/usr/lib/i386-linux-gnu/vapoursynth", directories);
        Assert.DoesNotContain("/home/u/.config/VapourSynth/plugins32", directories);
        Assert.Contains("/home/u/.config/VapourSynth/plugins64", directories);
    }

    [Fact]
    public void FilterDirectories_64Bit_KeepsPlainLibWhenNoTwinExists()
    {
        Assert.SkipUnless(Environment.Is64BitProcess, "64-bit process");
        var input = new[] { "/usr/lib/vapoursynth" };

        var directories = VsPathResolver.FilterDirectories(input);

        Assert.Equal(["/usr/lib/vapoursynth"], directories);
    }

    [Fact]
    public void GetInstallDirectories_Linux_IncludesCurrentMultiarch()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux library paths");

        var directories = VsPathResolver.GetInstallDirectories();

        var multiarch = VsPathResolver.LinuxMultiarchDirectory(RuntimeInformation.ProcessArchitecture);
        if (multiarch != null)
        {
            Assert.Contains(multiarch, directories);
        }
        if (Environment.Is64BitProcess)
        {
            Assert.Contains("/usr/lib64", directories);
        }
    }
}
