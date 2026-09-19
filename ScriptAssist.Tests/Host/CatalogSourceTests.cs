using HanumanInstitute.ScriptAssist.AviSynth;
using HanumanInstitute.ScriptAssist.VapourSynth;
using Moq;
using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests.Host;

public class CatalogSourceTests
{
    [Fact]
    public void Enumerate_VapourSynthDump_MapsCoreNameAndTitle()
    {
        var native = new Mock<IVapourSynthNativeCatalog>();
        native.Setup(n => n.Read()).Returns(
        [
            new VapourSynthFunction("bm3d", "BM3D", "clip:vnode", "clip:vnode;", "VapourSynth BM3D")
        ]);

        var symbols = new VapourSynthSymbolSource(native.Object).Enumerate();

        Assert.Contains(symbols, s => s.Kind == SymbolKind.Namespace && s.Name == "core.bm3d"
            && s.Title == "VapourSynth BM3D");
        var function = Assert.Single(symbols, s => s.Kind == SymbolKind.Function);
        Assert.Equal("core.bm3d.BM3D", function.Name);
        Assert.Equal("VapourSynth BM3D", function.Title);
    }

    [Fact]
    public void Enumerate_AviSynthDump_SetsGroupAndMergesScripts()
    {
        var native = new Mock<IAviSynthNativeCatalog>();
        native.Setup(n => n.Read()).Returns(
        [
            new AviSynthFilter("Crop", "c[left]i", "InternalFunctions"),
            new AviSynthFilter("AutoloadOnly", "c", "UserFunctions")
        ]);
        var folders = new Mock<IScriptDirectory>();
        folders.Setup(d => d.Roots()).Returns(["/plugins"]);
        folders.Setup(d => d.Files("/plugins", It.IsAny<IReadOnlyList<string>>()))
            .Returns(["/plugins/helpers.avsi"]);
        folders.Setup(d => d.TryRead("/plugins/helpers.avsi"))
            .Returns("function Helper(clip c) { c }\n");
        var includes = new Mock<IIncludeSource>();
        includes.Setup(s => s.Read(It.IsAny<string>(), It.IsAny<string?>())).Returns((IncludeFile?)null);

        var symbols = new AviSynthSymbolSource(native.Object, folders.Object, includes.Object)
            .Enumerate();

        Assert.Equal("Internal", Assert.Single(symbols, s => s.Name == "Crop").Group);
        Assert.Equal("User", Assert.Single(symbols, s => s.Name == "AutoloadOnly").Group);
        Assert.Contains(symbols, s => s.Name == "Helper");
    }
}
