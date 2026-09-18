using Xunit;

namespace HanumanInstitute.ApiAviSynth.Tests;

public class AvsScriptTests
{
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
