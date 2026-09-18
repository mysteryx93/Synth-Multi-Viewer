using Xunit;

namespace HanumanInstitute.ApiVapourSynth.Tests;

public class VsScriptTests
{
    [Fact]
    public void LoadFile_CompatBgr32_ThrowsNotSupportedException()
    {
        const string path = "unused.vpy";

        var act = () => VsScript.LoadFile(path, true);

        Assert.Throws<NotSupportedException>(act);
    }
}
