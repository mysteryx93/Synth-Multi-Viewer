using System.Runtime.InteropServices;
using Xunit;

namespace HanumanInstitute.ApiAviSynth.Tests;

public class AvsPathResolverTests
{
    [Fact]
    public void ResolveScriptPath_RelativePath_ReturnsAbsolutePath()
    {
        var relativePath = Path.Combine("scripts", "example.avs");

        var resolvedPath = AvsPathResolver.ResolveScriptPath(relativePath);

        Assert.Equal(Path.GetFullPath(relativePath), resolvedPath);
    }

    [Fact]
    public void ResolveScriptPath_AbsolutePath_PreservesAbsolutePath()
    {
        var absolutePath = Path.GetFullPath(Path.Combine("scripts", "example.avs"));

        var resolvedPath = AvsPathResolver.ResolveScriptPath(absolutePath);

        Assert.Equal(absolutePath, resolvedPath);
    }

    [Fact]
    public void ResolveScriptPath_EmptyPath_ThrowsArgumentException()
    {
        const string path = "";

        var act = () => AvsPathResolver.ResolveScriptPath(path);

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void GetBundledPluginDirectories_RootedLibrary_ReturnsSiblingPluginFolders()
    {
        var libraryPath = Path.Combine(Path.GetTempPath(), "avisynth", "libavisynth.so");
        var libDir = Path.GetDirectoryName(libraryPath)!;
        var parent = Directory.GetParent(libDir)!.FullName;

        var directories = AvsPathResolver.GetBundledPluginDirectories(libraryPath);

        Assert.Equal(
            [
                Path.Combine(libDir, "plugins+"),
                Path.Combine(libDir, "plugins"),
                Path.Combine(parent, "plugins+"),
                Path.Combine(parent, "plugins")
            ],
            directories);
    }

    [Fact]
    public void GetSystemPluginDirectories_Linux_IncludesDistroAndLocalPaths()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux plugin paths");

        var directories = AvsPathResolver.GetSystemPluginDirectories();

        Assert.Contains("/usr/lib/avisynth", directories);
        Assert.Contains("/usr/local/lib/avisynth", directories);
    }

    [Fact]
    public void ResolveExistingPath_RelativeLibraryName_ReturnsAbsoluteWhenInstalled()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux library paths");
        var relative = AvsPathResolver.LibraryFileNames[0];
        var installed = AvsPathResolver.GetInstallDirectories()
            .SelectMany(directory => AvsPathResolver.LibraryFileNames.Select(name => Path.Combine(directory, name)))
            .FirstOrDefault(File.Exists);
        Assert.SkipWhen(installed == null, "AviSynth is not installed in a known directory");

        var resolved = AvsPathResolver.ResolveExistingPath(relative);

        Assert.True(Path.IsPathRooted(resolved));
        Assert.True(File.Exists(resolved));
    }

    [Fact]
    public void ResolveExistingPath_FileOnDisk_ReturnsAbsolutePath()
    {
        var file = Path.GetTempFileName();
        try
        {
            var resolved = AvsPathResolver.ResolveExistingPath(file);

            Assert.Equal(Path.GetFullPath(file), resolved);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ResolveExistingPath_EmptyPath_ThrowsArgumentException()
    {
        const string path = "";

        var act = () => AvsPathResolver.ResolveExistingPath(path);

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void GetLibraryCandidates_OverrideFile_ReturnsOnlyThatFile()
    {
        var overridePath = Path.GetTempFileName();
        try
        {
            var candidates = AvsPathResolver.GetLibraryCandidates(overridePath);

            Assert.Equal([overridePath], candidates);
        }
        finally
        {
            File.Delete(overridePath);
        }
    }

    [Fact]
    public void LibraryFileNames_CurrentOs_ContainsAviSynth()
    {
        var names = AvsPathResolver.LibraryFileNames;

        Assert.Contains(names, name => name.Contains("avisynth", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(Architecture.Arm64, "/usr/lib/aarch64-linux-gnu/avisynth", true)]
    [InlineData(Architecture.Arm64, "/usr/lib64/avisynth", true)]
    [InlineData(Architecture.Arm64, "/usr/lib/avisynth", true)]
    [InlineData(Architecture.Arm64, "/usr/lib/x86_64-linux-gnu/avisynth", false)]
    [InlineData(Architecture.Arm64, "/usr/lib/i386-linux-gnu/avisynth", false)]
    [InlineData(Architecture.Arm64, "/usr/lib/arm-linux-gnueabihf/avisynth", false)]
    [InlineData(Architecture.Arm64, @"C:\Program Files (x86)\AviSynth+\plugins", false)]
    [InlineData(Architecture.Arm64, @"C:\Program Files (Arm)\AviSynth+\plugins", false)]
    [InlineData(Architecture.Arm, "/usr/lib/arm-linux-gnueabihf/avisynth", true)]
    [InlineData(Architecture.Arm, "/usr/lib/aarch64-linux-gnu/avisynth", false)]
    [InlineData(Architecture.Arm, "/usr/lib64/avisynth", false)]
    [InlineData(Architecture.Armv6, "/usr/lib/arm-linux-gnueabihf/avisynth", true)]
    [InlineData(Architecture.X64, "/usr/lib/aarch64-linux-gnu/avisynth", false)]
    [InlineData(Architecture.X86, "/usr/lib64/avisynth", false)]
    public void MatchesDirectory_Architecture_KeepsOnlyNativePaths(Architecture arch, string path, bool expected)
    {
        var matches = AvsPathResolver.MatchesDirectory(path, arch);

        Assert.Equal(expected, matches);
    }

    [Fact]
    public void FilterDirectories_Arm64_DropsPlainLibWhenLib64TwinExists()
    {
        var input =
            new[]
            {
                "/usr/lib/avisynth",
                "/usr/lib64/avisynth",
                "/usr/lib/aarch64-linux-gnu/avisynth",
                "/usr/lib/x86_64-linux-gnu/avisynth"
            };

        var directories = AvsPathResolver.FilterDirectories(input, Architecture.Arm64);

        Assert.Equal(
            ["/usr/lib64/avisynth", "/usr/lib/aarch64-linux-gnu/avisynth"],
            directories);
    }

    [Fact]
    public void FilterDirectories_64Bit_DropsForeignArchitectureAndLibTwin()
    {
        Assert.SkipUnless(Environment.Is64BitProcess, "64-bit process");
        var input = new[]
        {
            "/usr/lib/avisynth",
            "/usr/lib64/avisynth",
            "/usr/lib/i386-linux-gnu/avisynth",
            "/usr/lib/aarch64-linux-gnu/avisynth",
            @"C:\Program Files (x86)\AviSynth+\plugins"
        };

        var directories = AvsPathResolver.FilterDirectories(input);

        Assert.DoesNotContain("/usr/lib/avisynth", directories);
        Assert.Contains("/usr/lib64/avisynth", directories);
        Assert.DoesNotContain("/usr/lib/i386-linux-gnu/avisynth", directories);
        Assert.DoesNotContain(@"C:\Program Files (x86)\AviSynth+\plugins", directories);
        if (RuntimeInformation.ProcessArchitecture != Architecture.Arm64)
        {
            Assert.DoesNotContain("/usr/lib/aarch64-linux-gnu/avisynth", directories);
        }
    }
}
