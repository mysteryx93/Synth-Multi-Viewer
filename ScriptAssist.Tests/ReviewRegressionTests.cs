using System.Diagnostics.CodeAnalysis;
using HanumanInstitute.ScriptAssist.AviSynth;
using HanumanInstitute.ScriptAssist.VapourSynth;
using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests;

[SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken")]
public class ReviewRegressionTests
{
    private static readonly Symbol[] Vs =
    [
        new("core.std.Crop", ["clip:vnode", "left:int:opt", "right:int:opt"], ReturnType: "clip:vnode;"),
        new("core.std.BlankClip", ["width:int:opt", "height:int:opt"], ReturnType: "clip:vnode;"),
        new("core.std.AudioTrim", ["clip:anode", "first:int:opt"], ReturnType: "clip:anode;")
    ];

    private static LanguageService VsService(IncludeReader? read = null) =>
        new(new VapourSynthLanguage(read), new CatalogCache(() => []));

    private static LanguageService AvsService() =>
        new(new AviSynthLanguage(), new CatalogCache(() => []));

    [Fact]
    public void MultilineCallDoesNotTreatKeywordAsLocal()
    {
        var text = """
            clip = core.std.BlankClip(
            width=640
            )
            clip.
            """;
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");

        var locals = "clip = core.std.BlankClip(\nwidth=640\n)\nw";
        var names = VsService().Analyze(locals, locals.Length, Vs);
        Assert.DoesNotContain(names.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void StringAssignmentKeepsStringType()
    {
        const string text = "name = \"hello\"";
        var hover = VsService().Analyze(text, text.IndexOf("name", StringComparison.Ordinal) + 1, Vs).Hover;
        Assert.NotNull(hover);
        Assert.Equal("str", hover.Text);
    }

    [Fact]
    public void SameFileFunctionCompletesAndHasInsight()
    {
        var text = """
            def helper(clip, radius=1):
                return clip
            helper
            """;
        var complete = VsService().Analyze(text, text.Length, Vs);
        Assert.Contains(complete.Items, x => x.InsertionText == "helper");

        var call = text + "(";
        var insight = VsService().Analyze(call, call.Length, Vs).Insight;
        Assert.NotNull(insight);
        Assert.Equal("helper", insight.Overloads[0].Name);
        Assert.Contains("radius=1", insight.Overloads[0].Signature);
    }

    [Fact]
    public void ImportedFunctionHasInsightAndReturnType()
    {
        var helper = "def Filter(clip) -> vs.VideoNode:\n    return clip\n";
        var service = VsService((specifier, _) => specifier == "helper"
            ? new IncludeFile("/plugins/helper.py", helper)
            : null);
        var text = "from helper import Filter\nFilter(";
        var insight = service.Analyze(text, text.Length, Vs).Insight;
        Assert.NotNull(insight);
        Assert.Equal("Filter", insight.Overloads[0].Name);

        var typed = "from helper import Filter\nclip = Filter()\nclip.";
        var reply = service.Analyze(typed, typed.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void AnnotationOnlyVariableIsTyped()
    {
        var text = "clip: vs.VideoNode\nclip.";
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void RelativeImportsDoNotShareModuleIdentity()
    {
        IncludeReader read = (specifier, fromPath) =>
        {
            if (specifier is "a" or "b")
            {
                return new IncludeFile("/pkg/" + specifier + "/__init__.py", "from .helper import *\n");
            }

            if (specifier != ".helper" && specifier != "helper")
            {
                return null;
            }

            var dir = Path.GetDirectoryName(fromPath) ?? "";
            if (dir.EndsWith("/a", StringComparison.Ordinal))
            {
                return new IncludeFile("/pkg/a/helper.py", "def OnlyA(clip):\n    return clip\n");
            }

            if (dir.EndsWith("/b", StringComparison.Ordinal))
            {
                return new IncludeFile("/pkg/b/helper.py", "def OnlyB(clip):\n    return clip\n");
            }

            return null;
        };
        var service = VsService(read);
        var a = "import a\na.";
        var fromA = service.Analyze(a, a.Length, Vs);
        Assert.Contains(fromA.Items, x => x.InsertionText == "OnlyA");
        Assert.DoesNotContain(fromA.Items, x => x.InsertionText == "OnlyB");

        var b = "import b\nb.";
        var fromB = service.Analyze(b, b.Length, Vs);
        Assert.Contains(fromB.Items, x => x.InsertionText == "OnlyB");
        Assert.DoesNotContain(fromB.Items, x => x.InsertionText == "OnlyA");
    }

    [Fact]
    public void ImportsRespectScopeAndOrder()
    {
        var helper = "def Filter(clip):\n    return clip\n";
        var service = VsService((specifier, _) => specifier == "helper"
            ? new IncludeFile("/plugins/helper.py", helper)
            : null);

        var leaked = """
            def f():
                from helper import Filter
            Fil
            """;
        var outside = service.Analyze(leaked, leaked.Length, Vs);
        Assert.DoesNotContain(outside.Items, x => x.InsertionText == "Filter");

        var inside = """
            def f():
                from helper import Filter
                Fil
            """;
        var body = service.Analyze(inside, inside.Length, Vs);
        Assert.Contains(body.Items, x => x.InsertionText == "Filter");

        var rebound = """
            local = 2
            import helper as local
            local.
            """;
        var module = service.Analyze(rebound, rebound.Length, Vs);
        Assert.Contains(module.Items, x => x.InsertionText == "Filter");
        Assert.DoesNotContain(module.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void ParenthesizedAndDottedImports()
    {
        var helper = "def Filter(clip):\n    return clip\n";
        var service = VsService((specifier, _) => specifier is "helper" or "package.helper"
            ? new IncludeFile("/plugins/" + specifier.Replace('.', '/') + ".py", helper)
            : null);

        var multiline = """
            from helper import (
                Filter,
            )
            Fil
            """;
        var fromImport = service.Analyze(multiline, multiline.Length, Vs);
        Assert.Contains(fromImport.Items, x => x.InsertionText == "Filter");

        var dotted = "import package.helper\npackage.";
        var pkg = service.Analyze(dotted, dotted.Length, Vs);
        Assert.Contains(pkg.Items, x => x.InsertionText == "helper");
        Assert.DoesNotContain(pkg.Items, x => x.InsertionText == "Filter");

        var nested = "import package.helper\npackage.helper.";
        var members = service.Analyze(nested, nested.Length, Vs);
        Assert.Contains(members.Items, x => x.InsertionText == "Filter");
    }

    [Fact]
    public void ChainedCallAndSliceOfferNodeMembers()
    {
        var text = "core.std.BlankClip()[0:10].";
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "import");
    }

    [Fact]
    public void AviSynthContinuationUsesJoinedSource()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        var text = "last.\\\nCrop(";
        var insight = AvsService().Analyze(text, text.Length, [crop]).Insight;
        Assert.NotNull(insight);
        Assert.True(insight.ImplicitClip);
    }

    [Fact]
    public void RefreshInvalidatesIncludeSnapshots()
    {
        var current = "def Old():\n    return 1\n";
        IncludeReader read = (specifier, _) => specifier == "helper"
            ? new IncludeFile("/plugins/helper.py", current)
            : null;
        var catalog = new CatalogCache(() => Array.Empty<Symbol>());
        var service = new LanguageService(new VapourSynthLanguage(read), catalog);
        var text = "import helper as h\nh.";
        var native = Array.Empty<Symbol>();
        Assert.Contains(service.Analyze(text, text.Length, native).Items, x => x.InsertionText == "Old");
        current = "def New():\n    return 1\n";
        Assert.Contains(service.Analyze(text, text.Length, native).Items, x => x.InsertionText == "Old");
        service.Invalidate();
        var refreshed = service.Analyze(text, text.Length, native);
        Assert.Contains(refreshed.Items, x => x.InsertionText == "New");
        Assert.DoesNotContain(refreshed.Items, x => x.InsertionText == "Old");
    }

    [Fact]
    public void ArgumentCompletionTracksUsedNamesAndValuePosition()
    {
        var used = "clip = core.std.BlankClip()\nclip.std.Crop(left=1, ";
        var reply = VsService().Analyze(used, used.Length, Vs);
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "left=");
        Assert.Contains(reply.Items, x => x.InsertionText == "right=");

        var value = "clip = core.std.BlankClip()\nclip.std.Crop(left=";
        var inValue = VsService().Analyze(value, value.Length, Vs);
        Assert.DoesNotContain(inValue.Items, x => x.InsertionText == "left=");

        var cache = "core.clear_cache(";
        var empty = VsService().Analyze(cache, cache.Length, Vs);
        Assert.DoesNotContain(empty.Items, x => x.InsertionText == "parameters=");
    }

    [Fact]
    public void EqualityIsNotANamedArgument()
    {
        var text = "core.std.BlankClip(width == 640)";
        var hover = VsService().Analyze(text, text.IndexOf("width", StringComparison.Ordinal) + 1, Vs).Hover;
        Assert.True(hover == null || hover.Text != "width:int:opt");
    }

    [Fact]
    public void VideoNodeBoundCompletionOmitsAudioOnlyFilters()
    {
        var text = "clip = core.std.BlankClip()\nclip.std.";
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "AudioTrim");
    }

    [Fact]
    public void DefaultTypeRefIsUnknown()
    {
        Assert.True(default(TypeRef).IsUnknown);
        Assert.Equal("", default(TypeRef).Id);
    }

    [Fact]
    public void UnterminatedSingleLineStringRecoversOnNewline()
    {
        var text = "name = \"hello\nclip = core.std.BlankClip()\nclip.";
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == "std");
    }
}
