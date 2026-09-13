using Xunit;

namespace HanumanInstitute.ApiAviSynth.Tests;

public class AvsScriptTests
{
    [Fact]
    public void ResolveScriptPath_RelativePath_ReturnsAbsolutePath()
    {
        var relativePath = Path.Combine("scripts", "example.avs");

        var resolvedPath = AvsScript.ResolveScriptPath(relativePath);

        Assert.Equal(Path.GetFullPath(relativePath), resolvedPath);
    }

    [Fact]
    public void ResolveScriptPath_EmptyPath_ThrowsArgumentException()
    {
        var path = string.Empty;

        var action = () => AvsScript.ResolveScriptPath(path);

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void TryFindLibrary_WhenFound_ReturnsNonEmptyPath()
    {
        var found = AvsScript.TryFindLibrary(out var path);

        if (found)
        {
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(Path.IsPathRooted(path));
            Assert.True(File.Exists(path));
        }
        else
        {
            Assert.Null(path);
        }
    }

    [Fact]
    public void SetDllPath_OverrideFile_IsTriedFirst()
    {
        var overridePath = Path.GetTempFileName();

        try
        {
            AvsScript.SetDllPath(overridePath);
            var first = AvsPathResolver.GetLibraryCandidates(overridePath)[0];

            Assert.Equal(overridePath, first);
        }
        finally
        {
            AvsScript.SetDllPath(null);
            File.Delete(overridePath);
        }
    }

    [Fact]
    public void SetDllPath_MissingOverride_DoesNotFallBackToSystem()
    {
        try
        {
            AvsScript.SetDllPath(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "libavisynth.so"));

            var found = AvsScript.TryFindLibrary(out var path);

            Assert.False(found);
            Assert.Null(path);
        }
        finally
        {
            AvsScript.SetDllPath(null);
        }
    }

    [Fact]
    public void TryEvaluate_MissingOverride_ReturnsFalse()
    {
        try
        {
            AvsScript.SetDllPath(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "libavisynth.so"));

            var usable = AvsScript.TryEvaluate(out var error);

            Assert.False(usable);
            Assert.False(string.IsNullOrWhiteSpace(error));
        }
        finally
        {
            AvsScript.SetDllPath(null);
        }
    }
}
