using System.Diagnostics.CodeAnalysis;
using HanumanInstitute.ScriptAssist.AvaloniaEdit;
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
        var a = "import a\nimport b\na.";
        var fromA = service.Analyze(a, a.Length, Vs);
        Assert.Contains(fromA.Items, x => x.InsertionText == "OnlyA");
        Assert.DoesNotContain(fromA.Items, x => x.InsertionText == "OnlyB");

        var b = "import a\nimport b\nb.";
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
        Assert.True(hover == null || hover.Text != "int");
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

    [Fact]
    public void MultilineCallInsideFunctionKeepsScope()
    {
        var text = """
            def f(clip):
                result = core.std.BlankClip(
            width=640
                )
                clip.
            """;
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void ScopedImportAndNestedDefInferAssignment()
    {
        var helper = "def Filter(clip) -> vs.VideoNode:\n    return clip\n";
        var service = VsService((specifier, _) => specifier == "helper"
            ? new IncludeFile("/plugins/helper.py", helper)
            : null);

        var imported = """
            def f(clip):
                from helper import Filter
                result = Filter(clip)
                result.
            """;
        var fromImport = service.Analyze(imported, imported.Length, Vs);
        Assert.Contains(fromImport.Items, x => x.InsertionText == "std");

        var nested = """
            def outer(clip):
                def inner():
                    result = clip
                    result.
            """;
        var inner = VsService().Analyze(nested, nested.Length, Vs);
        Assert.Contains(inner.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void ScopedCoreAliasDoesNotLeak()
    {
        var text = """
            def f():
                from vapoursynth import core as local
                local.std.BlankClip()
            local.
            """;
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");

        var imported = """
            def f():
                import vapoursynth as local
            local.
            """;
        var module = VsService().Analyze(imported, imported.Length, Vs);
        Assert.DoesNotContain(module.Items, x => x.InsertionText == "core");
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void PythonContinuationsKeepTypes(string newline)
    {
        var assigned = "clip = \\" + newline + "core.std.BlankClip()" + newline + "clip.";
        var reply = VsService().Analyze(assigned, assigned.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == "std");

        var dotted = "clip = core.std.\\" + newline + "BlankClip()" + newline + "clip.";
        var dottedReply = VsService().Analyze(dotted, dotted.Length, Vs);
        Assert.Contains(dottedReply.Items, x => x.InsertionText == "std");

        var helper = "def Filter(clip):\n    return clip\n";
        var service = VsService((specifier, _) => specifier == "helper"
            ? new IncludeFile("/plugins/helper.py", helper)
            : null);
        var imported = "from helper import \\" + newline + "Filter" + newline + "Fil";
        var names = service.Analyze(imported, imported.Length, Vs);
        Assert.Contains(names.Items, x => x.InsertionText == "Filter");
    }

    [Fact]
    public async Task InvalidateDoesNotPublishStaleInFlightSnapshot()
    {
        var started = new ManualResetEventSlim(false);
        var proceed = new ManualResetEventSlim(false);
        var current = "def Old():\n    return 1\n";
        IncludeReader read = (specifier, _) =>
        {
            if (specifier != "helper")
            {
                return null;
            }

            var file = new IncludeFile("/plugins/helper.py", current);
            started.Set();
            proceed.Wait();
            return file;
        };
        var service = new LanguageService(new VapourSynthLanguage(read), new CatalogCache(() => []));
        var text = "import helper as h\nh.";
        var native = Array.Empty<Symbol>();
        var first = Task.Run(() => service.Analyze(text, text.Length, native));
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        current = "def New():\n    return 1\n";
        service.Invalidate();
        proceed.Set();
        await first;
        var next = service.Analyze(text, text.Length, native);
        Assert.Contains(next.Items, x => x.InsertionText == "New");
        Assert.DoesNotContain(next.Items, x => x.InsertionText == "Old");
    }

    [Fact]
    public void ThreeComponentImportKeepsIntermediateNamespace()
    {
        var helper = "def Filter(clip):\n    return clip\n";
        var service = VsService((specifier, _) => specifier == "pkg.sub.helper"
            ? new IncludeFile("/plugins/pkg/sub/helper.py", helper)
            : null);

        var mid = "import pkg.sub.helper\npkg.sub.";
        var nested = service.Analyze(mid, mid.Length, Vs);
        Assert.Contains(nested.Items, x => x.InsertionText == "helper");
        Assert.DoesNotContain(nested.Items, x => x.InsertionText == "Filter");

        var leaf = "import pkg.sub.helper\npkg.sub.helper.";
        var members = service.Analyze(leaf, leaf.Length, Vs);
        Assert.Contains(members.Items, x => x.InsertionText == "Filter");
    }

    [Fact]
    public void ArgumentCompletionUsesResolvedCallFrame()
    {
        var list = "core.std.Crop([";
        var inList = VsService().Analyze(list, list.Length, Vs);
        Assert.DoesNotContain(inList.Items, x => x.InsertionText == "clip=");
        Assert.DoesNotContain(inList.Items, x => x.InsertionText == "left=");

        var grouping = "core.std.Crop(left=(";
        var inValue = VsService().Analyze(grouping, grouping.Length, Vs);
        Assert.DoesNotContain(inValue.Items, x => x.InsertionText == "clip=");
        Assert.DoesNotContain(inValue.Items, x => x.InsertionText == "left=");
        Assert.DoesNotContain(inValue.Items, x => x.InsertionText == "right=");

        var call = """
            def f(clip, *, radius=2):
                return clip
            f(x, 
            """;
        var insight = VsService().Analyze(call, call.Length, Vs).Insight;
        Assert.NotNull(insight);
        Assert.Equal("Parameter 2: radius=2", OverloadProvider.ActiveParameterText(insight));
    }

    [Fact]
    public void AssignmentShadowsFunctionSymbol()
    {
        var text = """
            def Filter(clip) -> vs.VideoNode:
                return clip
            Filter = 2
            Filter(
            """;
        var insight = VsService().Analyze(text, text.Length, Vs).Insight;
        Assert.True(insight == null || insight.Overloads.All(x => x.Name != "Filter"));

        var helper = "def Filter(clip) -> vs.VideoNode:\n    return clip\n";
        var service = VsService((specifier, _) => specifier == "helper"
            ? new IncludeFile("/plugins/helper.py", helper)
            : null);
        var imported = "from helper import Filter\nFilter = 2\nFilter(";
        var rebound = service.Analyze(imported, imported.Length, Vs).Insight;
        Assert.True(rebound == null || rebound.Overloads.All(x => x.Name != "Filter"));
    }

    [Fact]
    public void LaterFunctionBindingReplacesEarlierValue()
    {
        var helper = "def Filter(clip) -> vs.VideoNode:\n    return clip\n";
        var service = VsService((specifier, _) => specifier == "helper"
            ? new IncludeFile("/plugins/helper.py", helper)
            : null);

        var imported = "Filter = 1\nfrom helper import Filter\nFilter(";
        var fromImport = service.Analyze(imported, imported.Length, Vs).Insight;
        Assert.NotNull(fromImport);
        Assert.Equal("Filter", fromImport.Overloads[0].Name);

        var defined = """
            Filter = 1
            def Filter(clip) -> vs.VideoNode:
                return clip
            Filter(
            """;
        var definedInsight = VsService().Analyze(defined, defined.Length, Vs).Insight;
        Assert.NotNull(definedInsight);
        Assert.Equal("Filter", definedInsight.Overloads[0].Name);
    }

    [Fact]
    public void InnerFunctionImportShadowsGlobalFunction()
    {
        IncludeReader read = (specifier, _) => specifier switch
        {
            "helper" => new IncludeFile("/plugins/helper.py",
                "def Filter(clip) -> vs.VideoNode:\n    return clip\n"),
            "other" => new IncludeFile("/plugins/other.py", "def Filter() -> int:\n    return 1\n"),
            _ => null
        };
        var service = VsService(read);
        var text = """
            from helper import Filter
            def f():
                from other import Filter
                x = Filter()
                x.
            """;
        var reply = service.Analyze(text, text.Length, Vs);
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void PackageAndSubmoduleImportsPreserveBothSides()
    {
        IncludeReader read = (specifier, _) => specifier switch
        {
            "pkg" => new IncludeFile("/plugins/pkg/__init__.py", "def RootFunction():\n    return 1\n"),
            "pkg.sub" => new IncludeFile("/plugins/pkg/sub/__init__.py", "def SubFunction():\n    return 1\n"),
            "pkg.sub.helper" => new IncludeFile("/plugins/pkg/sub/helper.py", "def HelperFn():\n    return 1\n"),
            _ => null
        };
        var service = VsService(read);

        foreach (var text in new[]
                 {
                     "import pkg\nimport pkg.sub\npkg.",
                     "import pkg.sub\nimport pkg\npkg."
                 })
        {
            var reply = service.Analyze(text, text.Length, Vs);
            Assert.Contains(reply.Items, x => x.InsertionText == "RootFunction");
            Assert.Contains(reply.Items, x => x.InsertionText == "sub");
        }

        var nested = "import pkg.sub\nimport pkg.sub.helper\npkg.sub.";
        var members = service.Analyze(nested, nested.Length, Vs);
        Assert.Contains(members.Items, x => x.InsertionText == "SubFunction");
        Assert.Contains(members.Items, x => x.InsertionText == "helper");
    }

    [Fact]
    public void ArgumentCompletionSkipsInvalidKeywordNames()
    {
        var avs = new[] { new Symbol("Foo", ["clip", "int"]) };
        var unnamed = AvsService().Analyze("Foo(", 4, avs);
        Assert.DoesNotContain(unnamed.Items, x => x.InsertionText == "clip=");
        Assert.DoesNotContain(unnamed.Items, x => x.InsertionText == "int=");

        var positional = "core.std.Crop(clip, ";
        var afterClip = VsService().Analyze(positional, positional.Length, Vs);
        Assert.DoesNotContain(afterClip.Items, x => x.InsertionText == "clip=");
        Assert.Contains(afterClip.Items, x => x.InsertionText == "left=");

        var positionalOnly = """
            def f(clip, /, radius=2):
                return clip
            f(
            """;
        var slash = VsService().Analyze(positionalOnly, positionalOnly.Length, Vs);
        Assert.DoesNotContain(slash.Items, x => x.InsertionText == "clip=");
        Assert.Contains(slash.Items, x => x.InsertionText == "radius=");

        var expression = "core.std.Crop(x + ";
        var inside = VsService().Analyze(expression, expression.Length, Vs);
        Assert.DoesNotContain(inside.Items, x => x.InsertionText == "clip=");
        Assert.DoesNotContain(inside.Items, x => x.InsertionText == "left=");
        Assert.DoesNotContain(inside.Items, x => x.InsertionText == "right=");
    }

    [Fact]
    public void ArgumentCompletionSkipsAlreadySuppliedSlots()
    {
        var text = """
            def f(path, radius=2):
                return path
            f("input.mkv", 
            """;
        var afterString = VsService().Analyze(text, text.Length, Vs);
        Assert.DoesNotContain(afterString.Items, x => x.InsertionText == "path=");
        Assert.Contains(afterString.Items, x => x.InsertionText == "radius=");

        var nested = """
            def f(path, radius=2):
                return path
            f(core.std.BlankClip(width=640), 
            """;
        var afterCall = VsService().Analyze(nested, nested.Length, Vs);
        Assert.DoesNotContain(afterCall.Items, x => x.InsertionText == "path=");
        Assert.Contains(afterCall.Items, x => x.InsertionText == "radius=");
    }

    [Fact]
    public void OneLineFunctionBodyStaysInScope()
    {
        var leak = """
            def f(clip): value = 1; leak = core.std.BlankClip()
            leak.
            """;
        var outside = VsService().Analyze(leak, leak.Length, Vs);
        Assert.DoesNotContain(outside.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(outside.Items, x => x.InsertionText == "width");

        var body = "def f(clip): value = core.std.BlankClip(); value.";
        var inside = VsService().Analyze(body, body.Length, Vs);
        Assert.Contains(inside.Items, x => x.InsertionText == "std");
        Assert.Contains(inside.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void UnpackingOverwritesExistingTypes()
    {
        var text = """
            clip = core.std.BlankClip()
            clip, value = other
            clip.
            """;
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void ParenthesizedKnownClipKeepsMembers()
    {
        var text = "clip = core.std.BlankClip()\n(clip).";
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void NestedDefHasLocalCallInsight()
    {
        var text = """
            def outer():
                def inner(clip, radius=2):
                    return clip
                inner(
            """;
        var insight = VsService().Analyze(text, text.Length, Vs).Insight;
        Assert.NotNull(insight);
        Assert.Equal("inner", insight.Overloads[0].Name);
        Assert.Contains("radius=2", insight.Overloads[0].Signature);
    }

    [Fact]
    public void ParameterDefaultInfersFromAssignedName()
    {
        var text = """
            base = core.std.BlankClip()
            def f(source=base):
                source.
            """;
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void RebindingModuleAliasDoesNotMergeUnrelatedModules()
    {
        IncludeReader read = (specifier, _) => specifier switch
        {
            "helper" => new IncludeFile("/plugins/helper.py", "def Filter(clip):\n    return clip\n"),
            "other" => new IncludeFile("/plugins/other.py", "def OtherFn():\n    return 1\n"),
            _ => null
        };
        var service = VsService(read);

        var rebound = """
            import helper as mod
            import other as mod
            mod.
            """;
        var members = service.Analyze(rebound, rebound.Length, Vs);
        Assert.Contains(members.Items, x => x.InsertionText == "OtherFn");
        Assert.DoesNotContain(members.Items, x => x.InsertionText == "Filter");

        var separate = """
            import other as o
            import helper as mod
            import other as mod
            o.
            """;
        var original = service.Analyze(separate, separate.Length, Vs);
        Assert.Contains(original.Items, x => x.InsertionText == "OtherFn");
        Assert.DoesNotContain(original.Items, x => x.InsertionText == "Filter");
    }

    [Fact]
    public void EscapedQuoteInDefaultKeepsReturnType()
    {
        var local = """
            def f(text="a\"b") -> vs.VideoNode:
                return clip
            clip = f()
            clip.
            """;
        var reply = VsService().Analyze(local, local.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");

        var helper = "def f(text=\"a\\\"b\") -> vs.VideoNode:\n    return clip\n";
        var service = VsService((specifier, _) => specifier == "helper"
            ? new IncludeFile("/plugins/helper.py", helper)
            : null);
        var imported = "from helper import f\nclip = f()\nclip.";
        var fromImport = service.Analyze(imported, imported.Length, Vs);
        Assert.Contains(fromImport.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void ClassMethodsAreNotModuleFunctions()
    {
        var text = """
            class C:
                def Apply(self, clip):
                    return clip
            App
            """;
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Apply");

        var inside = """
            class C:
                def Apply(self, clip):
                    clip.
            """;
        var members = VsService().Analyze(inside, inside.Length, Vs);
        Assert.Contains(members.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void FunctionBodyKeepsStatementOrder()
    {
        var assignment = """
            def f():
                make = 1
                def make() -> vs.VideoNode:
                    return clip
                result = make()
                result.
            """;
        var assigned = VsService().Analyze(assignment, assignment.Length, Vs);
        Assert.Contains(assigned.Items, x => x.InsertionText == "std");

        var helper = "def make() -> vs.VideoNode:\n    return clip\n";
        var imported = VsService((specifier, _) => specifier == "helper"
            ? new IncludeFile("/plugins/helper.py", helper)
            : null);
        var earlierImport = """
            def f():
                from helper import make
                def make() -> int:
                    return 1
                result = make()
                result.
            """;
        var shadowed = imported.Analyze(earlierImport, earlierImport.Length, Vs);
        Assert.DoesNotContain(shadowed.Items, x => x.InsertionText == "std");

        var nestedDefault = """
            def f():
                base = core.std.BlankClip()
                def inner(source=base):
                    source.
            """;
        var defaults = VsService().Analyze(nestedDefault, nestedDefault.Length, Vs);
        Assert.Contains(defaults.Items, x => x.InsertionText == "std");
        Assert.Contains(defaults.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void ClassBodyDoesNotLeakIntoModuleOrDropMethodLocals()
    {
        var leaked = """
            clip = core.std.BlankClip()
            class C:
                clip = 1
            clip.
            """;
        var reply = VsService().Analyze(leaked, leaked.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == "std");

        var helper = "def Filter():\n    return 1\n";
        var imported = VsService((specifier, _) => specifier == "helper"
            ? new IncludeFile("/plugins/helper.py", helper)
            : null);
        var classImport = """
            class C:
                import helper as h
            h.
            """;
        var members = imported.Analyze(classImport, classImport.Length, Vs);
        Assert.DoesNotContain(members.Items, x => x.InsertionText == "Filter");

        var nested = """
            class C:
                def g(self):
                    def make(clip) -> vs.VideoNode:
                        return clip
                    result = make(clip)
                    result.
            """;
        var inside = VsService().Analyze(nested, nested.Length, Vs);
        Assert.Contains(inside.Items, x => x.InsertionText == "std");
        Assert.Contains(inside.Items, x => x.InsertionText == "width");
        Assert.DoesNotContain(VsService().Analyze(nested + "\nm", (nested + "\nm").Length, Vs).Items,
            x => x.InsertionText == "make");
    }

    [Fact]
    public void PlaceholderPackageDoesNotMergeIntoUnrelatedAlias()
    {
        IncludeReader read = (specifier, _) => specifier switch
        {
            "pkg.sub" => new IncludeFile("/plugins/pkg/sub/__init__.py", "def SubFn():\n    return 1\n"),
            "other" => new IncludeFile("/plugins/other.py", "def OtherFn():\n    return 1\n"),
            _ => null
        };
        var text = "import pkg.sub\nimport other as pkg\npkg.";
        var reply = VsService(read).Analyze(text, text.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == "OtherFn");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "sub");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "SubFn");
    }

    [Fact]
    public void ParameterCompletionDoesNotInsertSecondEquals()
    {
        var text = """
            def f(radius=2):
                return radius
            f(radius=2)
            """;
        var caret = text.LastIndexOf("radius", StringComparison.Ordinal) + 3;
        var item = Assert.Single(VsService().Analyze(text, caret, Vs).Items,
            x => x.InsertionText is "radius" or "radius=");
        Assert.Equal("radius", item.InsertionText);
    }

    [Fact]
    public void ParameterCompletionSkipsNamesAfterExistingValue()
    {
        var text = """
            def f(path, radius=2):
                return path
            f("input.mkv"
            """;
        var reply = VsService().Analyze(text, text.Length, Vs);
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "path=");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "radius=");
    }

    [Fact]
    public void LaterDeclaredHelperTypesFunctionBody()
    {
        var text = """
            def f():
                result = make()
                result.
            def make() -> vs.VideoNode:
                return clip
            """;
        var caret = text.IndexOf("result.", StringComparison.Ordinal) + "result.".Length;
        var reply = VsService().Analyze(text, caret, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == "std");
        Assert.Contains(reply.Items, x => x.InsertionText == "width");

        var moduleLevel = """
            clip = make()
            clip.
            def make() -> vs.VideoNode:
                return clip
            """;
        var early = VsService().Analyze(moduleLevel, moduleLevel.IndexOf("clip.", StringComparison.Ordinal) + 5, Vs);
        Assert.DoesNotContain(early.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void StringWithOperatorKeepsStringType()
    {
        const string text = "name = \"a+b\"";
        var hover = VsService().Analyze(text, text.IndexOf("name", StringComparison.Ordinal) + 1, Vs).Hover;
        Assert.NotNull(hover);
        Assert.Equal("str", hover.Text);
    }

    [Fact]
    public void NativeReturnIdentifiersStayCompatible()
    {
        var text = """
            def as_str() -> str:
                return "x"
            def as_bool() -> bool:
                return True
            a = as_str()
            b = as_bool()
            """;
        var strHover = VsService().Analyze(text, text.IndexOf("a =", StringComparison.Ordinal) + 1, Vs).Hover;
        Assert.Equal("str", strHover?.Text);
        var boolHover = VsService().Analyze(text, text.IndexOf("b =", StringComparison.Ordinal) + 1, Vs).Hover;
        Assert.Equal("bool", boolHover?.Text);
    }

    [Fact]
    public void AviSynthAssignmentAfterStringOrEmptyRhsStillBinds()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        var blank = new Symbol("BlankClip", ["int [width]"]);
        var text = """
            path = "movie.mkv"
            clip = BlankClip()
            clip.
            """;
        var reply = AvsService().Analyze(text, text.Length, [crop, blank]);
        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");

        var unfinished = """
            path =
            clip = BlankClip()
            clip.
            """;
        var next = AvsService().Analyze(unfinished, unfinished.Length, [crop, blank]);
        Assert.Contains(next.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void UnclosedVapourSynthHeaderDoesNotHideLaterFunction()
    {
        var text = """
            def broken(clip
            def good(clip, radius=2):
                return clip
            good(
            """;
        var insight = VsService().Analyze(text, text.Length, Vs).Insight;
        Assert.NotNull(insight);
        Assert.Equal("good", insight.Overloads[0].Name);
        Assert.Contains("radius=2", insight.Overloads[0].Signature);

        var helper = """
            def broken(clip
            def Filter(clip, radius=2):
                return clip
            """;
        var imported = VsService((specifier, _) => specifier == "helper"
            ? new IncludeFile("/plugins/helper.py", helper)
            : null);
        var call = "from helper import Filter\nFilter(";
        var fromImport = imported.Analyze(call, call.Length, Vs).Insight;
        Assert.NotNull(fromImport);
        Assert.Equal("Filter", fromImport.Overloads[0].Name);
    }

    [Fact]
    public void UnclosedAviSynthHeaderDoesNotHideLaterCall()
    {
        var text = """
            function Broken(clip c,
            function Good(clip c) {
                return c
            }
            Good(
            """;
        var insight = AvsService().Analyze(text, text.Length, []).Insight;
        Assert.NotNull(insight);
        Assert.Equal("Good", insight.Overloads[0].Name);
    }

    [Fact]
    public void MismatchedDelimitersDoNotStealLaterCallOrAssignment()
    {
        var text = "core.std.Crop([0),\n";
        var closed = VsService().Analyze(text, text.Length, Vs);
        Assert.Null(closed.Insight);

        var later = """
            core.std.Crop([0)
            x = 1
            """;
        var assignment = VsService().Analyze(later, later.Length, Vs);
        Assert.Null(assignment.Insight);

        var crop = new Symbol("Crop", ["clip", "int [left]", "int [top]"]);
        var avs = "Crop([0),\n";
        var avsClosed = AvsService().Analyze(avs, avs.Length, [crop]);
        Assert.Null(avsClosed.Insight);

        var avsLater = """
            Crop([0)
            x = 1
            """;
        var avsAssignment = AvsService().Analyze(avsLater, avsLater.Length, [crop]);
        Assert.Null(avsAssignment.Insight);
    }

    [Fact]
    public void CallInsightUsesCommentMaskedStringsKeptSource()
    {
        var text = """
            core.std.Crop(
            # margins
            right=2
            """;
        var insight = VsService().Analyze(text, text.Length, Vs).Insight;
        Assert.NotNull(insight);
        Assert.Equal(2, insight.ActiveParameter);

        var empty = "core.std.Crop(\n# comment\n";
        var names = VsService().Analyze(empty, empty.Length, Vs);
        Assert.Contains(names.Items, x => x.InsertionText == "left=");
        Assert.Contains(names.Items, x => x.InsertionText == "right=");

        var avsCrop = new Symbol("Crop", ["clip", "int [left]", "int [right]"]);
        var avs = """
            Crop(
            # margins
            right=2
            """;
        var avsInsight = AvsService().Analyze(avs, avs.Length, [avsCrop]).Insight;
        Assert.NotNull(avsInsight);
        Assert.Equal(2, avsInsight.ActiveParameter);

        var avsEmpty = "Crop(\n# comment\n";
        var avsNames = AvsService().Analyze(avsEmpty, avsEmpty.Length, [avsCrop]);
        Assert.Contains(avsNames.Items, x => x.InsertionText == "left=");
        Assert.Contains(avsNames.Items, x => x.InsertionText == "right=");
    }

    [Fact]
    public void ImportedClassBodyImportsDoNotLeakIntoModuleExports()
    {
        var helper = "def Filter():\n    return 1\n";
        var wrapper = """
            class C:
                from helper import Filter
            def Keep():
                return 1
            """;
        IncludeReader read = (specifier, _) => specifier switch
        {
            "helper" => new IncludeFile("/plugins/helper.py", helper),
            "wrapper" => new IncludeFile("/plugins/wrapper.py", wrapper),
            _ => null
        };
        var text = "import wrapper\nwrapper.";
        var reply = VsService(read).Analyze(text, text.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == "Keep");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Filter");
    }

    [Fact]
    public void AnnotationWhitelistMapsQuotedOptionalAndNullableUnions()
    {
        foreach (var header in new[]
                 {
                     "def f(clip: 'vs.VideoNode'):",
                     "def f(clip: typing.Optional[vs.VideoNode]):",
                     "def f(clip: None | vs.VideoNode):",
                     "def f(clip: vs.VideoNode | None):"
                 })
        {
            var text = header + "\n    clip.";
            var reply = VsService().Analyze(text, text.Length, Vs);
            Assert.Contains(reply.Items, x => x.InsertionText == "std");
            Assert.Contains(reply.Items, x => x.InsertionText == "width");
        }

        var core = "def f(c: Core):\n    c.";
        Assert.Contains(VsService().Analyze(core, core.Length, Vs).Items, x => x.InsertionText == "num_threads");

        var frame = "def f(f: vs.VideoFrame):\n    f.";
        Assert.Contains(VsService().Analyze(frame, frame.Length, Vs).Items, x => x.InsertionText == "copy");
    }

    [Fact]
    public void HostMetadataCompletesFormatAndCopiedFrame()
    {
        var query = "core.query_video_format(";
        var insight = VsService().Analyze(query, query.Length, Vs).Insight;
        Assert.NotNull(insight);
        Assert.Contains(insight.Overloads[0].Parameters!, x => x.Contains("subsampling_w", StringComparison.Ordinal));

        var fmt = "fmt = core.query_video_format(vs.YUV, vs.INTEGER, 8)\nfmt.";
        var format = VsService().Analyze(fmt, fmt.Length, Vs);
        Assert.Contains(format.Items, x => x.InsertionText == "subsampling_w");
        Assert.Contains(format.Items, x => x.InsertionText == "name");

        var copied = "frame = core.std.BlankClip().get_frame(0).copy()\nframe.";
        var members = VsService().Analyze(copied, copied.Length, Vs);
        Assert.Contains(members.Items, x => x.InsertionText == "copy");
        Assert.Contains(members.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void KeywordParameterOffersTrailingUnderscore()
    {
        var expr = new Symbol("core.std.Expr", ["clip:vnode", "lambda:float:opt"], ReturnType: "clip:vnode;");
        var text = "core.std.Expr(";
        var reply = VsService().Analyze(text, text.Length, [expr]);
        Assert.Contains(reply.Items, x => x.InsertionText == "lambda_=");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "lambda=");

        var named = "core.std.Expr(lambda_=";
        var insight = VsService().Analyze(named, named.Length, [expr]).Insight;
        Assert.NotNull(insight);
        Assert.Equal(1, insight.ActiveParameter);
    }

    [Fact]
    public void DirectFunctionAliasKeepsInsightAndReturn()
    {
        var text = """
            crop = core.std.Crop
            crop(
            """;
        var insight = VsService().Analyze(text, text.Length, Vs).Insight;
        Assert.NotNull(insight);
        Assert.Equal("core.std.Crop", insight.Overloads[0].Name);

        var result = """
            crop = core.std.Crop
            clip = crop(core.std.BlankClip(), 0, 0, 0, 0)
            clip.
            """;
        var members = VsService().Analyze(result, result.Length, Vs);
        Assert.Contains(members.Items, x => x.InsertionText == "std");
        Assert.Contains(members.Items, x => x.InsertionText == "width");
    }
}
