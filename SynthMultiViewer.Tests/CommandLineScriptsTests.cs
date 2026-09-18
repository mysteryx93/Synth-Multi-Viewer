using HanumanInstitute.SynthMultiViewer.Helpers;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class CommandLineScriptsTests
{
    [Fact]
    public void FromArguments_HostAndScript_ReturnsScript()
    {
        var script = Path.Combine("media", "preview.vpy");

        var files = CommandLineScripts.FromArguments(["SynthMultiViewer", script]);

        Assert.Equal([script], files);
    }

    [Fact]
    public void FromArguments_DotnetHostDllAndScript_SkipsDll()
    {
        var script = Path.Combine("home", "clip.avs");

        var files = CommandLineScripts.FromArguments(["dotnet", "SynthMultiViewer.dll", script]);

        Assert.Equal([script], files);
    }

    [Fact]
    public void FromArguments_FlagsAndFileUri_ReturnsLocalPath()
    {
        var script = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "script.vpy"));

        var files = CommandLineScripts.FromArguments(["SynthMultiViewer", "--foo", new Uri(script).AbsoluteUri]);

        Assert.Equal([script], files);
    }

    [Fact]
    public void FromArguments_UnknownExtension_IsIgnored()
    {
        var script = Path.Combine("job.avsi");

        var files = CommandLineScripts.FromArguments(["viewer", "notes.txt", script]);

        Assert.Equal([script], files);
    }
}
