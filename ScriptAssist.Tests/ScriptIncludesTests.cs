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
    [InlineData("clip", null)]
    [InlineData("int", null)]
    [InlineData("float", null)]
    [InlineData("function", null)]
    [InlineData("any", null)]
    [InlineData("*args", null)]
    [InlineData("", null)]
    public void AviSynthParameterNames(string parameter, string? expected) =>
        Assert.Equal(expected, ParameterNames.OfAviSynth(parameter));

    [Theory]
    [InlineData("left:int:opt", "left")]
    [InlineData("radius=1", "radius")]
    [InlineData("radius: int = 1", "radius")]
    [InlineData("radius: Optional[int] = None", "radius")]
    [InlineData("planes=[0, 1]", "planes")]
    [InlineData("Preset='Slow'", "Preset")]
    [InlineData("clip", "clip")]
    [InlineData("*args", null)]
    [InlineData("", null)]
    public void PythonParameterNames(string parameter, string? expected) =>
        Assert.Equal(expected, ParameterNames.OfPython(parameter));

    [Theory]
    [InlineData("left:int:opt", "int")]
    [InlineData("clip:vnode:opt", "vnode")]
    [InlineData("format:int:opt", "int")]
    [InlineData("radius: int = 1", "int")]
    [InlineData("clip: vs.VideoNode", "vs.VideoNode")]
    [InlineData("radius: Optional[int] = None", "Optional[int]")]
    [InlineData("offsets:int[]", "int")]
    [InlineData("planes=[0, 1]", null)]
    [InlineData("clip", null)]
    public void PythonParameterTypes(string parameter, string? expected) =>
        Assert.Equal(expected, ParameterNames.PythonType(parameter));

    [Theory]
    [InlineData("int [height]", "int")]
    [InlineData("int [blksizev]", "int")]
    [InlineData("clip c", "clip")]
    [InlineData("int \"width\"", "int")]
    [InlineData("int* [planes]", "int")]
    [InlineData("string \"Preset\"", "string")]
    [InlineData("clip", "clip")]
    [InlineData("*args", null)]
    public void AviSynthParameterTypes(string parameter, string? expected) =>
        Assert.Equal(expected, ParameterNames.AviSynthType(parameter));

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
        Assert.Equal("TR0", ParameterNames.OfAviSynth(qtgmc.Parameters![1]));
        Assert.Equal("Preset", ParameterNames.OfAviSynth(qtgmc.Parameters[2]));
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
    public void AviSynthUnionKeepsNativeOverloads()
    {
        var native = new[]
        {
            new Symbol("Foo", ["clip", "int [a]"]),
            new Symbol("Foo", ["clip", "float [a]"])
        };
        var merged = AviSynthFunctions.UnionByName(native, []);
        Assert.Equal(2, merged.Count(x => x.Name == "Foo"));
    }

    [Fact]
    public void AviSynthUnionEnrichesMatchingSignaturesAndKeepsUnmatched()
    {
        var native = new[]
        {
            new Symbol("Foo", ["clip"]),
            new Symbol("Foo", ["int"])
        };
        var parsed = new[]
        {
            new Symbol("Foo", ["clip c"])
        };
        var merged = AviSynthFunctions.UnionByName(native, parsed);
        Assert.Equal(2, merged.Count(x => x.Name == "Foo"));
        Assert.Contains(merged, x => x.Name == "Foo" && x.Parameters is ["clip c"]);
        Assert.Contains(merged, x => x.Name == "Foo" && x.Parameters is ["int"]);
    }

    [Fact]
    public void AviSynthUnionKeepsIncompatibleSingleNativeSignature()
    {
        var native = new[] { new Symbol("Foo", ["int"]) };
        var parsed = new[] { new Symbol("Foo", ["clip c"]) };
        var merged = AviSynthFunctions.UnionByName(native, parsed);
        var foo = Assert.Single(merged, x => x.Name == "Foo");
        Assert.Equal(["int"], foo.Parameters!);
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
