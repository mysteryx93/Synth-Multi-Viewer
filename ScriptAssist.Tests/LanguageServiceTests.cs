using System.Diagnostics.CodeAnalysis;
using HanumanInstitute.ScriptAssist.AviSynth;
using HanumanInstitute.ScriptAssist.VapourSynth;
using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests;

[SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken")]
public class LanguageServiceTests
{
    private static readonly Symbol[] Vs =
    [
        new("core.std.Crop", ["clip:vnode", "left:int:opt", "right:int:opt"], ReturnType: "clip:vnode;"),
        new("core.std.BlankClip", ["width:int:opt", "height:int:opt"], ReturnType: "clip:vnode;"),
        new("core.rife.RIFE", ["clip:vnode", "model:int:opt"], ReturnType: "clip:vnode;"),
        new("core.std.SelectEvery", ["clip:vnode", "cycle:int", "offsets:int[]"], ReturnType: "clip:vnode;"),
        new("core.svp1.Super", ["clip:vnode"], ReturnType: "clip:vnode;clip:vnode;")
    ];

    private static LanguageService VsService() =>
        new(new VapourSynthLanguage(), new CatalogCache(() => []));

    private static LanguageService AvsService() =>
        new(new AviSynthLanguage(), new CatalogCache(() => []));

    [Theory]
    [InlineData("core.", "std")]
    [InlineData("core.std.", "Crop")]
    [InlineData("core.rife.", "RIFE")]
    [InlineData("vs.core.std.Cr", "Crop")]
    [InlineData("vs.", "core")]
    [InlineData("co", "core")]
    [InlineData("c = vs.core\nc.", "std")]
    [InlineData("c = vs.core\nc.std.", "Crop")]
    [InlineData("c = vs.get_core()\nc.rife.", "RIFE")]
    [InlineData("c = vs.core  # alias\nc.std.Cr", "Crop")]
    [InlineData("import vapoursynth as vpy\nvpy.", "core")]
    [InlineData("import vapoursynth as vpy\nvpy.core.std.", "Crop")]
    [InlineData("from vapoursynth import core as c\nc.std.", "Crop")]
    [InlineData("from vapoursynth import core\nc = core\nc.std.", "Crop")]
    public void VapourSynthPaths(string text, string expected)
    {
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == expected);
    }

    [Fact]
    public void VapourSynthClipAssignmentOffersBoundPlugins()
    {
        var text = "clip = vs.core.std.BlankClip()\nclip.";
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void VapourSynthStatementStartDoesNotLeakCatalogFunctions()
    {
        var reply = VsService().Analyze("Cr", 2, Vs);
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void VapourSynthChainAfterCallOffersBoundPlugin()
    {
        var text = "clip = core.std.BlankClip()\nclip.std.Crop(0, 0, 2, 2).std.";
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void VapourSynthSliceKeepsNodeType()
    {
        var text = "clip = core.std.BlankClip()\nclip[0:10].";
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void VapourSynthAddKeepsNodeType()
    {
        var text = "a = core.std.BlankClip()\nb = a + a\nb.";
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void VapourSynthAnnotationTypesParameter()
    {
        var text = "def f(clip: vs.VideoNode):\n    clip.";
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void VapourSynthClipParameterAndLiteralsAreTyped()
    {
        var clipDef = "def f(clip):\n    clip.";
        var clip = VsService().Analyze(clipDef, clipDef.Length, Vs);
        Assert.Contains(clip.Items, x => x.InsertionText == "std");

        var radiusDef = "def f(radius=1):\n    radius";
        var radius = VsService().Analyze(radiusDef, radiusDef.Length, Vs).Hover;
        Assert.NotNull(radius);
        Assert.Equal("int", radius.Text);

        var flagged = "def f(flag=True):\n    flag";
        var boolean = VsService().Analyze(flagged, flagged.Length, Vs).Hover;
        Assert.NotNull(boolean);
        Assert.Equal("bool", boolean.Text);

        var named = "def f(name=\"x\"):\n    name";
        var text = VsService().Analyze(named, named.Length, Vs).Hover;
        Assert.NotNull(text);
        Assert.Equal("str", text.Text);

        var number = "n = 4\nn";
        var integer = VsService().Analyze(number, number.Length, Vs).Hover;
        Assert.NotNull(integer);
        Assert.Equal("int", integer.Text);

        var real = "n = 1.5\nn";
        var floating = VsService().Analyze(real, real.Length, Vs).Hover;
        Assert.NotNull(floating);
        Assert.Equal("float", floating.Text);
    }

    [Fact]
    public void VapourSynthFunctionParameterDoesNotLeak()
    {
        var crop = "def f(clip):\n    return clip\nclip.";
        var leaked = VsService().Analyze(crop, crop.Length, Vs);
        Assert.DoesNotContain(leaked.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(leaked.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void VapourSynthAnnotationHoverIsTypeOnlyAndHeaderParamsAreSilent()
    {
        const string header = "def f(clip: vs.VideoNode):";
        Assert.Null(VsService().Analyze(header, header.IndexOf("clip", StringComparison.Ordinal) + 1, Vs).Hover);
        Assert.Null(VsService().Analyze(header, header.IndexOf("VideoNode", StringComparison.Ordinal) + 1, Vs).Hover);
        var module = VsService().Analyze(header, header.IndexOf("vs.", StringComparison.Ordinal) + 1, Vs).Hover;
        Assert.NotNull(module);
        Assert.Equal("vapoursynth", module.Text);

        var body = header + "\n    clip";
        var clip = VsService().Analyze(body, body.Length, Vs).Hover;
        Assert.NotNull(clip);
        Assert.Equal("VideoNode", clip.Text);

        var local = "def f(clip):\n    cl";
        var item = Assert.Single(VsService().Analyze(local, local.Length, Vs).Items, x => x.InsertionText == "clip");
        Assert.Equal(SymbolKind.Local, item.Kind);
        Assert.Equal("clip: VideoNode", item.Signature);

        var constant = "vs.FL";
        var flt = Assert.Single(VsService().Analyze(constant, constant.Length, Vs).Items,
            x => x.InsertionText == "FLOAT");
        Assert.Equal(SymbolKind.Property, flt.Kind);
        Assert.Equal("FLOAT: int", flt.Signature);
        Assert.Equal("int", VsService().Analyze("vs.FLOAT", "vs.FLOAT".Length, Vs).Hover?.Text);
    }

    [Fact]
    public void VapourSynthIndentAndClassMethodKeepClipScope()
    {
        var nested = """
            def f(clip):
                if True:
                    clip.
            """;
        Assert.Contains(VsService().Analyze(nested, nested.Length, Vs).Items, x => x.InsertionText == "std");

        var method = """
            class C:
                def g(self, clip):
                    clip.
            """;
        Assert.Contains(VsService().Analyze(method, method.Length, Vs).Items, x => x.InsertionText == "std");

        var after = """
            class C:
                def g(self, clip):
                    return clip
            clip.
            """;
        var leaked = VsService().Analyze(after, after.Length, Vs);
        Assert.DoesNotContain(leaked.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void VapourSynthMultiKeyReturnStaysUnknown()
    {
        var text = "super = core.svp1.Super()\nsuper.";
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void VapourSynthTupleAssignmentIsNotRoot()
    {
        var text = "_MV_BLK = (4, 8, 16, 32, 64, 128)\n_MV_BLK";
        var hover = VsService().Analyze(text, text.Length, Vs).Hover;
        Assert.Null(hover);

        var wrapped = "clip = (core.std.BlankClip())\nclip";
        var clip = VsService().Analyze(wrapped, wrapped.Length, Vs).Hover;
        Assert.NotNull(clip);
        Assert.Contains("VideoNode", clip.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void VapourSynthHoverShowsVideoNode()
    {
        var text = "clip = core.std.BlankClip()\nclip";
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.NotNull(reply.Hover);
        Assert.Contains("VideoNode", reply.Hover.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void VapourSynthHoverDistinguishesCoreAndPlugin()
    {
        const string text = "core.std";
        var core = VsService().Analyze(text, 2, Vs).Hover;
        Assert.NotNull(core);
        Assert.Contains("Core", core.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("plugin", core.Text, StringComparison.OrdinalIgnoreCase);

        var plugin = VsService().Analyze(text, text.Length, Vs).Hover;
        Assert.NotNull(plugin);
        Assert.Contains("plugin", plugin.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(": Core", plugin.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void VapourSynthPropertyIsNotUnknownParameters()
    {
        var assigned = "clip = core.std.BlankClip()\nclip.";
        var width = Assert.Single(VsService().Analyze(assigned, assigned.Length, Vs).Items,
            x => x.InsertionText == "width");
        Assert.Equal("width: int", width.Signature);
        Assert.DoesNotContain("parameters unknown", width.Signature, StringComparison.Ordinal);

        var hoverText = assigned + "width";
        var hover = VsService().Analyze(hoverText, hoverText.Length, Vs).Hover;
        Assert.NotNull(hover);
        Assert.Equal("int", hover.Text);
    }

    [Fact]
    public void VapourSynthFpsPropertyShowsFraction()
    {
        var text = "clip = core.std.BlankClip()\nclip.fps";
        var hover = VsService().Analyze(text, text.Length, Vs).Hover;
        Assert.NotNull(hover);
        Assert.Equal("Fraction", hover.Text);
        var items = VsService().Analyze(text[..^3], text.Length - 3, Vs).Items;
        var fps = Assert.Single(items, x => x.InsertionText == "fps");
        Assert.Equal("fps: Fraction", fps.Signature);
    }

    [Fact]
    public void VapourSynthNamedArgumentHoverIsParameterNotProperty()
    {
        const string text = "core.std.BlankClip(width=640)";
        var caret = text.IndexOf("width", StringComparison.Ordinal) + 1;
        var hover = VsService().Analyze(text, caret, Vs).Hover;
        Assert.NotNull(hover);
        Assert.Equal("width:int:opt", hover.Text);
        Assert.DoesNotContain("width: int", hover.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void VapourSynthArgumentCompletionKeepsInsight()
    {
        const string text = "core.std.Crop(co";
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.NotNull(reply.Insight);
        Assert.Equal("core.std.Crop", reply.Insight.Overloads[0].Name);
        Assert.Contains(reply.Items, x => x.InsertionText == "core");
    }

    [Theory]
    [InlineData("clip:vnode;")]
    [InlineData("clip:vnode")]
    public void VapourSynthNativeReturnTerminatorTypesClip(string returnType)
    {
        var catalog = new[] { new Symbol("core.std.BlankClip", ["width:int:opt"], ReturnType: returnType) };
        var assigned = "clip = core.std.BlankClip()\nclip";
        Assert.Contains(VsService().Analyze(assigned + ".", assigned.Length + 1, catalog).Items,
            x => x.InsertionText == "width");
        var hover = VsService().Analyze(assigned, assigned.Length, catalog).Hover;
        Assert.NotNull(hover);
        Assert.Contains("VideoNode", hover.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void VapourSynthKeywordArgumentCompletion()
    {
        var text = "clip = core.std.BlankClip()\nclip.std.Crop(le";
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == "left=");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "clip=");
    }

    [Fact]
    public void VapourSynthAliasedCallShowsInsight()
    {
        var text = "c = vs.core\nc.std.Crop(";
        var insight = VsService().Analyze(text, text.Length, Vs).Insight!;
        Assert.Equal("core.std.Crop", insight.Overloads[0].Name);
        Assert.Equal(0, insight.ActiveParameter);
        Assert.False(insight.ImplicitClip);
    }

    [Fact]
    public void VapourSynthBoundCallSkipsFirstNode()
    {
        var text = "clip = core.std.BlankClip()\nclip.std.Crop(";
        var insight = VsService().Analyze(text, text.Length, Vs).Insight!;
        Assert.True(insight.ImplicitClip);
        Assert.Equal(0, insight.ActiveParameter);
    }

    [Fact]
    public void VapourSynthNamedArgumentInsightSelectsParameter()
    {
        var catalog = new[]
        {
            new Symbol("core.std.BlankClip", ["clip:vnode:opt", "width:int:opt", "height:int:opt"],
                ReturnType: "clip:vnode;")
        };
        const string text = "core.std.BlankClip(width=";
        var insight = VsService().Analyze(text, text.Length, catalog).Insight!;
        Assert.Equal(1, insight.ActiveParameter);
        Assert.False(insight.ImplicitClip);
        Assert.Equal("width:int:opt", insight.Overloads[0].Parameters![1]);
    }

    [Fact]
    public void VapourSynthBoundNamedArgumentInsightSkipsClip()
    {
        const string text = "clip = core.std.BlankClip()\nclip.std.Crop(left=";
        var insight = VsService().Analyze(text, text.Length, Vs).Insight!;
        Assert.True(insight.ImplicitClip);
        Assert.Equal(0, insight.ActiveParameter);
    }

    [Theory]
    [InlineData("clip = core.std.BlankClip()\ncl", "clip")]
    [InlineData("src, dst = clip, clip\nds", "dst")]
    [InlineData("import vapoursynth as vpy\nvp", "vpy")]
    [InlineData("from vapoursynth import core as c\ncore.std.Crop(c", "c")]
    [InlineData("clip = core.std.BlankClip(width=320)\nw", "while")]
    public void BufferNamesCompleteAsLocals(string text, string expected)
    {
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == expected);
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void AviSynthGlobalAssignmentCompletes()
    {
        var text = "global foo = last\nfo";
        var reply = AvsService().Analyze(text, text.Length, []);
        Assert.Contains(reply.Items, x => x.InsertionText == "foo");
    }

    [Fact]
    public void AviSynthNamedArgumentHoverIsParameterNotInternal()
    {
        var blank = new Symbol("BlankClip", ["int [width]", "int [height]"]);
        var height = new Symbol("Height", ["clip"]);
        var text = "BlankClip(height=480)";
        var caret = text.IndexOf("height", StringComparison.Ordinal) + 1;
        var hover = AvsService().Analyze(text, caret, [blank, height]).Hover;
        Assert.NotNull(hover);
        Assert.Equal("int [height]", hover.Text);
        Assert.DoesNotContain("Height(", hover.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AviSynthNamedArgumentOfUnknownCallIsNotAVariable()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        var width = new Symbol("Width", ["clip"]);
        var text = """
            function Foo(clip C, int blkSize) {
                C.StripeMaskPass(blksize=blkSize
            """;
        var caret = text.IndexOf("blksize=", StringComparison.Ordinal) + 1;
        var hover = AvsService().Analyze(text, caret, [crop, width]).Hover;
        Assert.Null(hover);

        var assigned = "blkSize = Width()";
        var variable = AvsService().Analyze(assigned, assigned.IndexOf("blkSize", StringComparison.Ordinal) + 1,
            [crop, width]).Hover;
        Assert.NotNull(variable);
        Assert.Equal("int", variable.Text);
    }

    [Fact]
    public void AviSynthParameterTypeHoverIsNotConversionFunction()
    {
        var conversion = new Symbol("String", ["val"]);
        var clip = new Symbol("Clip", ["clip"]);
        var text = """
            function Foo(clip C, string "Preset") {
                return C
            }
            """;
        var stringHover = AvsService().Analyze(text, text.IndexOf("string", StringComparison.Ordinal) + 1,
            [conversion, clip]).Hover;
        Assert.Null(stringHover);
        var clipType = AvsService().Analyze(text, text.IndexOf("clip", StringComparison.Ordinal) + 1, [conversion, clip])
            .Hover;
        Assert.Null(clipType);
        var parameter = AvsService().Analyze(text, text.IndexOf("C,", StringComparison.Ordinal), [conversion, clip]).Hover;
        Assert.Null(parameter);

        const string call = "n = String(5)";
        var called = AvsService().Analyze(call, call.IndexOf("String", StringComparison.Ordinal) + 1, [conversion]).Hover;
        Assert.NotNull(called);
        Assert.Contains("String(", called.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AviSynthPropertyHoverStillShowsInternal()
    {
        var height = new Symbol("Height", ["clip"]);
        var text = "last.Height";
        var hover = AvsService().Analyze(text, text.Length, [height]).Hover;
        Assert.NotNull(hover);
        Assert.Contains("Height", hover.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AviSynthLastOffersClipTakingFilters()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]", "int [top]"]);
        var blank = new Symbol("BlankClip", ["int [width]"]);
        var text = "last.";
        var reply = AvsService().Analyze(text, text.Length, [crop, blank]);
        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "BlankClip");
    }

    [Fact]
    public void AviSynthUnknownIdentifierDotIsSilent()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        var reply = AvsService().Analyze("foo.", 4, [crop]);
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void AviSynthFunctionParameterIsTypedLocal()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        var width = new Symbol("Width", ["clip"]);
        var text = """
            function Foo(clip C, int "amount") {
                C.
            """;
        var reply = AvsService().Analyze(text, text.Length, [crop, width]);
        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");

        var hoverAt = text.LastIndexOf('C');
        var hover = AvsService().Analyze(text, hoverAt, [crop, width]).Hover;
        Assert.NotNull(hover);
        Assert.Equal("clip", hover.Text);

        var assigned = """
            function Foo(clip C) {
                nw = C.Width()
                nw.
            """;
        var nw = AvsService().Analyze(assigned, assigned.Length, [crop, width]);
        Assert.DoesNotContain(nw.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void AviSynthFunctionScopeDoesNotLeak()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        var closed = """
            function Foo(clip C) {
                inner = C
                return inner
            }
            C.
            """;
        var leaked = AvsService().Analyze(closed, closed.Length, [crop]);
        Assert.DoesNotContain(leaked.Items, x => x.InsertionText == "Crop");

        var inner = """
            function Foo(clip C) {
                inner = C
                return inner
            }
            inner.
            """;
        var innerDot = AvsService().Analyze(inner, inner.Length, [crop]);
        Assert.DoesNotContain(innerDot.Items, x => x.InsertionText == "Crop");

        var fromScript = """
            src = last
            function Foo(clip C) {
                src.
            """;
        var visible = AvsService().Analyze(fromScript, fromScript.Length, [crop]);
        Assert.Contains(visible.Items, x => x.InsertionText == "Crop");

        var lifted = """
            function Foo(clip C) {
                global g = C
            }
            g.
            """;
        var globalClip = AvsService().Analyze(lifted, lifted.Length, [crop]);
        Assert.Contains(globalClip.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void AviSynthContinuedHeadersAndDefaultKeepClip()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        var width = new Symbol("Width", ["clip"]);
        var trailing = """
            function Foo(clip clp, \
                int "n")
            {
                clp = Default(clp, last)
                clp.
            """;
        var reply = AvsService().Analyze(trailing, trailing.Length, [crop, width]);
        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");

        var leading = """
            function Foo(clip C,
            \ int "n")
            {
                C.
            """;
        var lead = AvsService().Analyze(leading, leading.Length, [crop]);
        Assert.Contains(lead.Items, x => x.InsertionText == "Crop");

        var property = """
            function Foo(clip C) {
                n = C.Width
                n.
            """;
        var typed = AvsService().Analyze(property, property.Length, [crop, width]);
        Assert.DoesNotContain(typed.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void AviSynthHeaderDoesNotShowCallInsightOrSwallowNextFunction()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        var foo = new Symbol("Foo", ["clip C", "int n"]);
        var header = "function Foo(clip C,";
        Assert.Null(AvsService().Analyze(header, header.Length, [foo, crop]).Insight);

        var split = """
            function Foo(clip C)
            function Bar(clip D) {
                D.
            """;
        var reply = AvsService().Analyze(split, split.Length, [crop]);
        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");
        var afterFoo = """
            function Foo(clip C)
            function Bar(clip D) {
                return D
            }
            C.
            """;
        var leaked = AvsService().Analyze(afterFoo, afterFoo.Length, [crop]);
        Assert.DoesNotContain(leaked.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void AviSynthImportedFunctionParametersAreNotLocals()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        var avsi = "function QTGMC(clip Input, int \"TR0\") { return Input }\n";
        IncludeReader read = (specifier, _) =>
            specifier == "QTGMC.avsi" ? new IncludeFile("/plugins/QTGMC.avsi", avsi) : null;
        var service = new LanguageService(new AviSynthLanguage(read), new CatalogCache(() => []));
        var text = "Import(\"QTGMC.avsi\")\nInput.";
        var reply = service.Analyze(text, text.Length, [crop]);
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void AviSynthContinuedAndTernaryAssignmentsKeepClipType()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]", "int [top]"]);
        var continued = """
            src = last.Crop(0, 0, \
            2, 2)
            src.
            """;
        var reply = AvsService().Analyze(continued, continued.Length, [crop]);
        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");

        var ternary = "src = true ? last : last.Crop(0, 0, 2, 2)\nsrc.";
        var typed = AvsService().Analyze(ternary, ternary.Length, [crop]);
        Assert.Contains(typed.Items, x => x.InsertionText == "Crop");

        var wrapped = "src = (FrameDouble ? Interleave(last, last) : last)\nsrc.";
        var wrappedDot = AvsService().Analyze(wrapped, wrapped.Length, [crop]);
        Assert.Contains(wrappedDot.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void AviSynthStarBracketCommentAndTripleQuoteAssignmentStayClip()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]", "int [top]"]);
        var text = """"
            M = last
            Flow = last
            R= (Oput==O_AUTO)                               [** auto: artifact masking     *]
            \     ? (FrameDouble ? Interleave(C, SelectOdd(M)) : M)
            \ : (Oput==O_FLOW)                              [** flow: interpolation only   *]
            \     ? Flow
            \ : last
            R = Debug ? R.GScriptClip("""Skip = EMskip.AverageLuma()
            \           Subtitle("BlkSize: " + string(BlkSize)
            \           , lsp=0)""", args = "EM", Local=true) : R
            R.
            """";
        var reply = AvsService().Analyze(text, text.Length, [crop]);
        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");

        var hoverAt = text.LastIndexOf("R.", StringComparison.Ordinal);
        var hover = AvsService().Analyze(text, hoverAt, [crop]).Hover;
        Assert.NotNull(hover);
        Assert.Equal("clip", hover.Text);
    }

    [Fact]
    public void AviSynthInternalsDoNotTypeAsClip()
    {
        var width = new Symbol("Width", ["clip"]);
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        var text = "n = Width()\nn.";
        var reply = AvsService().Analyze(text, text.Length, [width, crop]);
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
    }

    [Theory]
    [InlineData("# core.std.", false)]
    [InlineData("\"core.std.", false)]
    [InlineData("'''core.std.\n", false)]
    [InlineData("/* Crop(\n", true)]
    [InlineData("/[ Crop(\n", true)]
    [InlineData("[* Crop(\n", true)]
    public void CommentsAndStringsSuppressCompletion(string text, bool avs)
    {
        var service = avs ? AvsService() : VsService();
        var result = service.Analyze(text, text.Length, Vs);
        Assert.Empty(result.Items);
        Assert.Null(result.Insight);
    }

    [Fact]
    public void NestedInsightTracksInnerThenOuterAndIgnoresStringCommas()
    {
        var text = "core.std.Crop(core.std.BlankClip(10, ";
        var inner = VsService().Analyze(text, text.Length, Vs).Insight!;
        Assert.Equal("core.std.BlankClip", inner.Overloads[0].Name);
        Assert.Equal(1, inner.ActiveParameter);
        text += "20), 'a,b', ";
        var outer = VsService().Analyze(text, text.Length, Vs).Insight!;
        Assert.Equal("core.std.Crop", outer.Overloads[0].Name);
        Assert.Equal(2, outer.ActiveParameter);
    }

    [Theory]
    [InlineData("Crop(10,", 1)]
    [InlineData("Crop(10, ", 1)]
    [InlineData("Crop(10, 20,", 2)]
    [InlineData("Crop(10, 20, ", 2)]
    [InlineData("last.Crop(10, ", 1)]
    public void AviSynthCommaAdvancesParameterIncludingTrailingSpace(string text, int parameter)
    {
        var crop = new Symbol("Crop", ["clip", "int [left]", "int [top]", "int [right]", "int [bottom]"]);
        var insight = AvsService().Analyze(text, text.Length, [crop]).Insight!;
        Assert.Equal("Crop", insight.Overloads[0].Name);
        Assert.Equal(parameter, insight.ActiveParameter);
    }

    [Fact]
    public void AviSynthBufferFunctionsAreLexedWithoutLoadingBufferPlugins()
    {
        var text = """
            # function Fake() {}
            /* function Fake2() {} */
            LoadPlugin("only-in-buffer.dll")
            function LocalFilter(
                clip c,
                int "amount" # argument comment
                ) { return c }
            last.LocalF
            """;
        var result = AvsService().Analyze(text, text.Length, []);
        var item = Assert.Single(result.Items);
        Assert.Equal("LocalFilter", item.InsertionText);
        Assert.Contains("amount", item.Signature);
        Assert.DoesNotContain("argument comment", item.Signature);
        text = text[..^6] + "LocalFilter(";
        var insight = AvsService().Analyze(text, text.Length, []).Insight!;
        Assert.True(insight.ImplicitClip);
        Assert.Contains("amount", insight.Overloads[0].Signature);
    }

    [Theory]
    [InlineData("ci[left]i[top]i", "clip,int,int [left],int [top]")]
    [InlineData("c[planes]i*", "clip,int* [planes]")]
    [InlineData("c[items]a", "clip,array [items]")]
    [InlineData("", "")]
    public void ParseAviSynthParameters(string format, string expected) =>
        Assert.Equal(expected, string.Join(",", AviSynth.AviSynthParameters.Parse(format)!));

    [Fact]
    public void UnknownParametersStayUnknown()
    {
        Assert.Null(AviSynth.AviSynthParameters.Parse(null));
        Assert.Null(AviSynth.AviSynthParameters.Parse("c[broken"));
        Assert.Null(AviSynth.AviSynthParameters.Parse("z"));
    }

    [Fact]
    public async Task EnumerationFailureIsCachedUntilPathChangeOrRefresh()
    {
        var count = 0;
        var cache = new CatalogCache(() =>
        {
            Interlocked.Increment(ref count);
            throw new InvalidOperationException();
        });
        cache.Refresh("path1");
        var service = new LanguageService(new VapourSynthLanguage(), cache);
        for (var i = 0; i < 5; i++)
        {
            Assert.Contains((await service.GetAsync("im", 2, CancellationToken.None)).Items,
                x => x.InsertionText == "import");
            cache.Refresh("path1");
        }

        Assert.Equal(1, count);
        cache.Refresh("path2");
        await cache.GetAsync(CancellationToken.None);
        Assert.Equal(2, count);
        cache.Refresh("path2", true);
        await cache.GetAsync(CancellationToken.None);
        Assert.Equal(3, count);
    }

    [Fact]
    public void VapourSynthOpenIndexDoesNotDumpRootMembers()
    {
        var text = "clip = core.std.BlankClip()\nclip[";
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "import");
        Assert.Null(reply.Insight);
    }

    [Fact]
    public void VapourSynthSelectEveryOpenIndexKeepsCallInsight()
    {
        var text = "clip = core.std.BlankClip()\nclip.std.SelectEvery(5, [";
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.NotNull(reply.Insight);
        Assert.Equal("core.std.SelectEvery", reply.Insight.Overloads[0].Name);
        Assert.Equal(1, reply.Insight.ActiveParameter);
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "import");
    }

    [Fact]
    public void VapourSynthClosedSliceStillOffersBoundPlugins()
    {
        var text = "clip = core.std.BlankClip()\nclip[0:10].";
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void VapourSynthImportedModuleMembersComplete()
    {
        var havs = """
            def QTGMC(clip, Preset='Slow', TR0=0):
                return clip

            def helper():
                def nested(clip):
                    return clip
                return nested
            """;
        var service = new LanguageService(new VapourSynthLanguage(HavsReader(havs)), new CatalogCache(() => []));
        var text = "import havsfunc as haf\nhaf.";
        var reply = service.Analyze(text, text.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == "QTGMC");
        Assert.Contains(reply.Items, x => x.InsertionText == "helper");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "nested");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");

        var call = text + "QTGMC(";
        var insight = service.Analyze(call, call.Length, Vs).Insight!;
        Assert.Equal("QTGMC", insight.Overloads[0].Name);
        Assert.Contains("Preset='Slow'", insight.Overloads[0].Signature);
    }

    [Fact]
    public void VapourSynthFromImportStarOffersFunctionsAtRoot()
    {
        var havs = "def QTGMC(clip, Preset='Slow'):\n    return clip\n";
        var service = new LanguageService(new VapourSynthLanguage(HavsReader(havs)), new CatalogCache(() => []));
        var text = "from havsfunc import *\nQTG";
        var reply = service.Analyze(text, text.Length, Vs);
        var item = Assert.Single(reply.Items, x => x.InsertionText == "QTGMC");
        Assert.Contains("Preset='Slow'", item.Signature);

        var hoverText = "from havsfunc import *\nQTGMC";
        var hover = service.Analyze(hoverText, hoverText.Length, Vs).Hover;
        Assert.NotNull(hover);
        Assert.Contains("QTGMC(", hover.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void VapourSynthUnknownImportStaysSilent()
    {
        var service = new LanguageService(new VapourSynthLanguage(HavsReader(null)), new CatalogCache(() => []));
        var text = "import os\nos.";
        var reply = service.Analyze(text, text.Length, Vs);
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "QTGMC");
        Assert.Empty(reply.Items);
    }

    [Fact]
    public void VapourSynthNestedImportExposesModuleAndSignatures()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pack"] = "import mid as m\ndef PackFn(clip):\n    return clip\n",
            ["mid"] = "import leaf as l\ndef MidFn(clip):\n    return clip\n",
            ["leaf"] = "def Deep(clip, radius=1):\n    return clip\n"
        };
        var service = new LanguageService(new VapourSynthLanguage(FilesReader(files)), new CatalogCache(() => []));
        var text = "import pack as p\np.";
        var reply = service.Analyze(text, text.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == "PackFn");
        Assert.Contains(reply.Items, x => x.InsertionText == "m");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Deep");

        var nested = "import pack as p\np.m.l.";
        var deep = service.Analyze(nested, nested.Length, Vs);
        Assert.Contains(deep.Items, x => x.InsertionText == "Deep");

        var call = nested + "Deep(";
        var insight = service.Analyze(call, call.Length, Vs).Insight!;
        Assert.Equal("Deep", insight.Overloads[0].Name);
        Assert.Contains("radius=1", insight.Overloads[0].Signature);
    }

    [Fact]
    public void VapourSynthReexportedFromImportCompletesOnImporter()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pack"] = "from mid import Deep\n",
            ["mid"] = "def Deep(clip, radius=1):\n    return clip\n"
        };
        var service = new LanguageService(new VapourSynthLanguage(FilesReader(files)), new CatalogCache(() => []));
        var text = "import pack as p\np.";
        var reply = service.Analyze(text, text.Length, Vs);
        var deep = Assert.Single(reply.Items, x => x.InsertionText == "Deep");
        Assert.Contains("radius=1", deep.Signature);
    }

    [Fact]
    public void VapourSynthFromImportStarFollowsNestedStar()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pack"] = "from mid import *\n",
            ["mid"] = "from leaf import *\n",
            ["leaf"] = "def Deep(clip, radius=1):\n    return clip\n"
        };
        var service = new LanguageService(new VapourSynthLanguage(FilesReader(files)), new CatalogCache(() => []));
        var text = "from pack import *\nDee";
        var reply = service.Analyze(text, text.Length, Vs);
        var deep = Assert.Single(reply.Items, x => x.InsertionText == "Deep");
        Assert.Contains("radius=1", deep.Signature);
    }

    [Fact]
    public void VapourSynthRelativeFromImportReadsSiblingModule()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["havsfunc"] = "from .qtgmc import QTGMC\n",
            [".qtgmc"] = "def QTGMC(clip, Preset='Slow'):\n    return clip\n"
        };
        var service = new LanguageService(new VapourSynthLanguage(FilesReader(files)), new CatalogCache(() => []));
        var text = "import havsfunc as haf\nhaf.";
        var reply = service.Analyze(text, text.Length, Vs);
        var qtgmc = Assert.Single(reply.Items, x => x.InsertionText == "QTGMC");
        Assert.Contains("Preset='Slow'", qtgmc.Signature);
    }

    [Fact]
    public void AviSynthImportAddsParsedSignatures()
    {
        var avsi = """
            function QTGMC(clip Input, int "TR0", string "Preset") {
                return Input
            }
            """;
        IncludeReader read = (specifier, _) =>
            specifier == "QTGMC.avsi" ? new IncludeFile("/plugins/QTGMC.avsi", avsi) : null;
        var service = new LanguageService(new AviSynthLanguage(read), new CatalogCache(() => []));
        var native = new[] { new Symbol("QTGMC", ["clip"]) };
        var text = "Import(\"QTGMC.avsi\")\nlast.QTG";
        var reply = service.Analyze(text, text.Length, native);
        var item = Assert.Single(reply.Items, x => x.InsertionText == "QTGMC");
        Assert.Contains("TR0", item.Signature);
        Assert.Contains("Preset", item.Signature);

        var call = "Import(\"QTGMC.avsi\")\nlast.QTGMC(";
        var insight = service.Analyze(call, call.Length, native).Insight!;
        Assert.True(insight.ImplicitClip);
        Assert.Contains("Preset", insight.Overloads[0].Signature);
    }

    [Fact]
    public void AviSynthNestedImportLoadsGrandchildSignatures()
    {
        IncludeReader read = (specifier, _) => specifier switch
        {
            "a.avs" => new IncludeFile("/a.avs", "Import(\"b.avs\")\nfunction FromA(clip c) { return c }"),
            "b.avs" => new IncludeFile("/b.avs", "Import(\"c.avs\")\nfunction FromB(clip c) { return c }"),
            "c.avs" => new IncludeFile("/c.avs",
                "function FromC(clip Input, int \"radius\") { return Input }"),
            _ => null
        };
        var service = new LanguageService(new AviSynthLanguage(read), new CatalogCache(() => []));
        var text = "Import(\"a.avs\")\nlast.";
        var reply = service.Analyze(text, text.Length, []);
        Assert.Contains(reply.Items, x => x.InsertionText == "FromA");
        Assert.Contains(reply.Items, x => x.InsertionText == "FromB");
        var fromC = Assert.Single(reply.Items, x => x.InsertionText == "FromC");
        Assert.Contains("radius", fromC.Signature);

        var call = "Import(\"a.avs\")\nlast.FromC(";
        var insight = service.Analyze(call, call.Length, []).Insight!;
        Assert.Contains("radius", insight.Overloads[0].Signature);
    }

    [Fact]
    public void AviSynthImportCycleDoesNotRecurseForever()
    {
        IncludeReader read = (specifier, _) => specifier switch
        {
            "a.avs" => new IncludeFile("/a.avs", "Import(\"b.avs\")\nfunction FromA(clip c) { return c }"),
            "b.avs" => new IncludeFile("/b.avs", "Import(\"a.avs\")\nfunction FromB(clip c) { return c }"),
            _ => null
        };
        var service = new LanguageService(new AviSynthLanguage(read), new CatalogCache(() => []));
        var text = "Import(\"a.avs\")\nlast.";
        var reply = service.Analyze(text, text.Length, []);
        Assert.Contains(reply.Items, x => x.InsertionText == "FromA");
        Assert.Contains(reply.Items, x => x.InsertionText == "FromB");
    }

    private static IncludeReader HavsReader(string? text) =>
        FilesReader(text == null ? [] : new Dictionary<string, string> { ["havsfunc"] = text });

    private static IncludeReader FilesReader(IReadOnlyDictionary<string, string> files) =>
        (specifier, _) => files.TryGetValue(specifier, out var text)
            ? new IncludeFile("/plugins/" + specifier.TrimStart('.') + ".py", text)
            : null;
}
