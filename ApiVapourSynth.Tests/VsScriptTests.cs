using Xunit;

namespace HanumanInstitute.ApiVapourSynth.Tests;

public class VsScriptTests
{
    [Fact]
    public void ResolveScriptPath_RelativePath_ReturnsAbsolutePath()
    {
        var relativePath = Path.Combine("scripts", "example.vpy");

        var resolvedPath = VsScript.ResolveScriptPath(relativePath);

        Assert.Equal(Path.GetFullPath(relativePath), resolvedPath);
    }

    [Fact]
    public void LoadFile_CompatBgr32_ThrowsNotSupportedException()
    {
        var path = "unused.vpy";

        var action = () => VsScript.LoadFile(path, true);

        Assert.Throws<NotSupportedException>(action);
    }
}
