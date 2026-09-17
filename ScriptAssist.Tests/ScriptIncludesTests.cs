using System.Diagnostics.CodeAnalysis;
using HanumanInstitute.ScriptAssist.AviSynth;
using HanumanInstitute.ScriptAssist.VapourSynth;
using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests;

[SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken")]
public class ScriptIncludesTests
{
    [Theory]
    [InlineData("int [left]", "left")]
    [InlineData("int \"width\"", "width")]
    [InlineData("clip c", "c")]
    [InlineData("clip Input", "Input")]
    [InlineData("left:int:opt", "left")]
    [InlineData("radius=1", "radius")]
    [InlineData("radius: int = 1", "radius")]
    [InlineData("Preset='Slow'", "Preset")]
    [InlineData("clip", "clip")]
    [InlineData("*args", null)]
    [InlineData("", null)]
    public void ParameterNameOfKnownForms(string parameter, string? expected) =>
        Assert.Equal(expected, ParameterNames.Of(parameter));

    [Fact]
    public void ParameterSplitKeepsNestedCommas()
    {
        var parts = ParameterNames.Split("clip c, int [left], val \"a, b\", radius: int = (1, 2)");
        Assert.Equal(["clip c", "int [left]", "val \"a, b\"", "radius: int = (1, 2)"], parts);
    }

    [Fact]
    public void AviSynthParseReadsQuotedAndTypedHeaders()
    {
        var text = """
            # function Hidden() { }
            function QTGMC(clip Input, int "TR0", string "Preset") {
                return Input
            }
            """;
        var symbols = AviSynthFunctions.Parse(text, new AviSynthLanguage().Lexer);
        var qtgmc = Assert.Single(symbols);
        Assert.Equal("QTGMC", qtgmc.Name);
        Assert.Equal(["clip Input", "int \"TR0\"", "string \"Preset\""], qtgmc.Parameters!);
        Assert.Equal("TR0", ParameterNames.Of(qtgmc.Parameters![1]));
        Assert.Equal("Preset", ParameterNames.Of(qtgmc.Parameters[2]));
        Assert.True(AviSynthTypes.TakesClip(qtgmc));
    }

    [Fact]
    public void AviSynthUnionPrefersParsedWhenNativeHasNoNames()
    {
        var native = new[]
        {
            new Symbol("QTGMC", ["clip"]),
            new Symbol("Crop", ["clip", "int [left]", "int [top]"])
        };
        var parsed = new[]
        {
            new Symbol("QTGMC", ["clip Input", "int \"TR0\""]),
            new Symbol("Crop", ["clip c"])
        };
        var merged = AviSynthFunctions.UnionByName(native, parsed);
        var qtgmc = Assert.Single(merged, x => x.Name == "QTGMC");
        Assert.Equal(["clip Input", "int \"TR0\""], qtgmc.Parameters!);
        var crop = Assert.Single(merged, x => x.Name == "Crop");
        Assert.Equal(["clip", "int [left]", "int [top]"], crop.Parameters!);
    }

    [Fact]
    public void VapourSynthParseIgnoresNestedDefs()
    {
        var text = """
            def QTGMC(clip, Preset='Slow'):
                return clip

            class Wrapper:
                def method(self, clip):
                    return clip
            """;
        var symbols = VapourSynthFunctions.Parse(text, new VapourSynthLanguage().Lexer);
        var qtgmc = Assert.Single(symbols);
        Assert.Equal("QTGMC", qtgmc.Name);
        Assert.Equal(["clip", "Preset='Slow'"], qtgmc.Parameters!);
    }

    [Fact]
    public void IncludePathsPythonPrefersBufferThenPluginRoots()
    {
        var fromPath = Path.Combine("/scripts", "job.vpy");
        var roots = new[] { "/plugins" };
        var paths = IncludePaths.PythonModule("havsfunc", fromPath, roots).ToArray();
        Assert.Contains(Path.Combine("/scripts", "havsfunc.py"), paths);
        Assert.Contains(Path.Combine("/scripts", "havsfunc", "__init__.py"), paths);
        Assert.Contains(Path.Combine("/plugins", "havsfunc.py"), paths);
        Assert.Contains(Path.Combine("/plugins", "havsfunc", "__init__.py"), paths);
    }

    [Fact]
    public void IncludePathsPythonRelativeUsesFileDirectory()
    {
        var fromPath = Path.Combine("/plugins", "havsfunc", "__init__.py");
        var paths = IncludePaths.PythonModule(".qtgmc", fromPath, ["/unused"]).ToArray();
        Assert.Equal(Path.Combine("/plugins", "havsfunc", "qtgmc.py"), paths[0]);
        Assert.Equal(Path.Combine("/plugins", "havsfunc", "qtgmc", "__init__.py"), paths[1]);
        Assert.DoesNotContain(paths, path => path.Contains("unused", StringComparison.Ordinal));
    }

    [Fact]
    public void IncludePathsAviSynthUsesSpecifierNextToDocument()
    {
        var fromPath = Path.Combine("/scripts", "job.avs");
        var paths = IncludePaths.AviSynth("helpers.avsi", fromPath, ["/plugins"]).ToArray();
        Assert.Equal(Path.Combine("/scripts", "helpers.avsi"), paths[0]);
        Assert.Contains(Path.Combine("/plugins", "helpers.avsi"), paths);
    }
}
