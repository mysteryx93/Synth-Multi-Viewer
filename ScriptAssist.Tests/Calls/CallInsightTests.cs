using System.Diagnostics.CodeAnalysis;
using HanumanInstitute.ScriptAssist.AvaloniaEdit;
using HanumanInstitute.ScriptAssist.AviSynth;
using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests.Calls;

using static AssistHarness;

[SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken")]
public class CallInsightTests
{
    [Fact]
    public void Hover_NamedArgument_ShowsParameterType()
    {
        const string text = "core.std.BlankClip(width=640)";
        var caret = text.IndexOf("width", StringComparison.Ordinal) + 1;

        var hover = VsService().Analyze(text, caret, Vs).Hover;

        Assert.NotNull(hover);
        Assert.Equal("int", hover.Text);
        Assert.DoesNotContain("width", hover.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Hover_ArrayParameter_ShowsArrayType()
    {
        const string text = "core.std.BlankClip(color=[32, 96, 192])";
        var caret = text.IndexOf("color", StringComparison.Ordinal) + 1;

        var hover = VsService().Analyze(text, caret, Vs).Hover;

        Assert.NotNull(hover);
        Assert.Equal("float[]", hover.Text);
    }

    [Fact]
    public void Hover_NamedArgumentAndValue_ShowType()
    {
        const string text = "w = 640\ncore.std.BlankClip(width=w)";
        var caret = text.IndexOf("width=", StringComparison.Ordinal) + 1;

        var name = VsService().Analyze(text, caret, Vs).Hover;

        Assert.Equal("int", name?.Text);
    }

    [Fact]
    public void Hover_NamedArgumentValue_ShowsType()
    {
        const string text = "w = 640\ncore.std.BlankClip(width=w)";
        var caret = text.IndexOf("=w", StringComparison.Ordinal) + 2;

        var value = VsService().Analyze(text, caret, Vs).Hover;

        Assert.Equal("int", value?.Text);
    }

    [Fact]
    public void Hover_BoundNamedArgument_ShowsTypeOnly()
    {
        var point = new Symbol("core.resize.Point",
            ["clip:vnode", "width:int:opt", "height:int:opt", "format:int:opt"], ReturnType: "clip:vnode;");
        var catalog = Vs.Concat([point]).ToArray();
        const string text = "clip = core.std.BlankClip()\nclip.resize.Point(format=vs.YUV420P8)";
        var caret = text.IndexOf("format=", StringComparison.Ordinal) + 1;

        var hover = VsService().Analyze(text, caret, catalog).Hover;

        Assert.NotNull(hover);
        Assert.Equal("VideoFormat", hover.Text);
    }

    [Fact]
    public void Insight_ArgumentCompletion_KeepsInsight()
    {
        const string text = "core.std.Crop(co";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.NotNull(reply.Insight);
        Assert.Equal("core.std.Crop", reply.Insight.Overloads[0].Name);
        Assert.Contains(reply.Items, x => x.InsertionText == "core");
    }

    [Fact]
    public void Complete_AviSynthFunctionAndAny_AreNotKeywordNames()
    {
        var native = new[] { new Symbol("Foo", AviSynthParameters.Parse("n.")!) };

        var reply = AvsService().Analyze("Foo(", 4, native);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "function=");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "any=");
    }

    [Fact]
    public void Complete_KeywordArgument_OffersParameterName()
    {
        const string text = "clip = core.std.BlankClip()\nclip.std.Crop(le";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "left=");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "clip=");
    }

    [Fact]
    public void Insight_AliasedCall_ShowsOverload()
    {
        const string text = "c = vs.core\nc.std.Crop(";

        var insight = VsService().Analyze(text, text.Length, Vs).Insight!;

        Assert.Equal("core.std.Crop", insight.Overloads[0].Name);
        Assert.Equal(0, insight.ActiveParameter);
        Assert.False(insight.ImplicitClip);
    }

    [Fact]
    public void Insight_BoundCall_SkipsFirstNode()
    {
        const string text = "clip = core.std.BlankClip()\nclip.std.Crop(";

        var insight = VsService().Analyze(text, text.Length, Vs).Insight!;

        Assert.True(insight.ImplicitClip);
        Assert.Equal(1, insight.ActiveParameter);
    }

    [Fact]
    public void Insight_NamedArgument_SelectsParameter()
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
    public void Insight_BoundNamedArgument_SkipsClip()
    {
        const string text = "clip = core.std.BlankClip()\nclip.std.Crop(left=";

        var insight = VsService().Analyze(text, text.Length, Vs).Insight!;

        Assert.True(insight.ImplicitClip);
        Assert.Equal(1, insight.ActiveParameter);
    }

    [Fact]
    public void Hover_AviSynthNamedArgument_ShowsParameterType()
    {
        var blank = new Symbol("BlankClip", ["int [width]", "int [height]"]);
        var height = new Symbol("Height", ["clip"]);
        const string text = "BlankClip(height=480)";
        var caret = text.IndexOf("height", StringComparison.Ordinal) + 1;

        var hover = AvsService().Analyze(text, caret, [blank, height]).Hover;

        Assert.NotNull(hover);
        Assert.Equal("int", hover.Text);
        Assert.DoesNotContain("height", hover.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Height(", hover.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Hover_AviSynthNamedArgumentAndValue_ShowType()
    {
        var msuper = new Symbol("MSuper", ["clip", "int [blksize]", "int [blksizev]"]);
        const string text = """
            function Foo(clip C, int blkSizeV) {
                C.MSuper(blksizev=blkSizeV)
            }
            """;
        var caret = text.IndexOf("blksizev=", StringComparison.Ordinal) + 1;

        var name = AvsService().Analyze(text, caret, [msuper]).Hover;

        Assert.Equal("int", name?.Text);
    }

    [Fact]
    public void Hover_AviSynthNamedArgumentValue_ShowsType()
    {
        var msuper = new Symbol("MSuper", ["clip", "int [blksize]", "int [blksizev]"]);
        const string text = """
            function Foo(clip C, int blkSizeV) {
                C.MSuper(blksizev=blkSizeV)
            }
            """;
        var caret = text.IndexOf("=blkSizeV", StringComparison.Ordinal) + 2;

        var value = AvsService().Analyze(text, caret, [msuper]).Hover;

        Assert.Equal("int", value?.Text);
    }

    [Fact]
    public void Hover_AviSynthUnknownCallNamedArgument_IsSilent()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        var width = new Symbol("Width", ["clip"]);
        const string text = """
            function Foo(clip C, int blkSize) {
                C.StripeMaskPass(blksize=blkSize
            """;
        var caret = text.IndexOf("blksize=", StringComparison.Ordinal) + 1;

        var hover = AvsService().Analyze(text, caret, [crop, width]).Hover;

        Assert.Null(hover);
    }

    [Fact]
    public void Hover_AviSynthWidthAssignment_ShowsInt()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        var width = new Symbol("Width", ["clip"]);
        const string assigned = "blkSize = Width()";
        var caret = assigned.IndexOf("blkSize", StringComparison.Ordinal) + 1;

        var variable = AvsService().Analyze(assigned, caret, [crop, width]).Hover;

        Assert.NotNull(variable);
        Assert.Equal("int", variable.Text);
    }

    [Fact]
    public void Insight_NestedCalls_TracksInner()
    {
        const string text = "core.std.Crop(core.std.BlankClip(10, ";

        var inner = VsService().Analyze(text, text.Length, Vs).Insight!;

        Assert.Equal("core.std.BlankClip", inner.Overloads[0].Name);
        Assert.Equal(1, inner.ActiveParameter);
    }

    [Fact]
    public void Insight_NestedCalls_TracksOuter()
    {
        const string text = "core.std.Crop(core.std.BlankClip(10, 20), 'a,b', ";

        var outer = VsService().Analyze(text, text.Length, Vs).Insight!;

        Assert.Equal("core.std.Crop", outer.Overloads[0].Name);
        Assert.Equal(2, outer.ActiveParameter);
    }

    [Theory]
    [InlineData("Crop(", 1)]
    [InlineData("Crop(10,", 2)]
    [InlineData("Crop(10, ", 2)]
    [InlineData("Crop(10, 20,", 3)]
    [InlineData("Crop(10, 20, ", 3)]
    [InlineData("last.Crop(10, ", 2)]
    public void Insight_AviSynthComma_AdvancesParameter(string text, int parameter)
    {
        var crop = new Symbol("Crop", ["clip", "int [left]", "int [top]", "int [right]", "int [bottom]"]);

        var insight = AvsService().Analyze(text, text.Length, [crop]).Insight!;

        Assert.Equal("Crop", insight.Overloads[0].Name);
        Assert.Equal(parameter, insight.ActiveParameter);
    }

    [Fact]
    public void Insight_OpenIndex_KeepsEnclosingCall()
    {
        const string text = "clip = core.std.BlankClip()\nclip.std.SelectEvery(5, [";

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.NotNull(reply.Insight);
        Assert.Equal("core.std.SelectEvery", reply.Insight.Overloads[0].Name);
        Assert.Equal(2, reply.Insight.ActiveParameter);
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "import");
    }

    [Fact]
    public void Complete_SameFileFunction_OffersName()
    {
        const string text = """
            def helper(clip, radius=1):
                return clip
            helper
            """;

        var complete = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(complete.Items, x => x.InsertionText == "helper");
    }

    [Fact]
    public void Insight_SameFileFunction_ShowsOverload()
    {
        const string text = """
            def helper(clip, radius=1):
                return clip
            helper
            """;
        const string call = text + "(";

        var insight = VsService().Analyze(call, call.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal("helper", insight.Overloads[0].Name);
        Assert.Contains("radius=1", insight.Overloads[0].Signature);
    }

    [Fact]
    public void Insight_AviSynthContinuation_UsesJoinedSource()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        const string text = "last.\\\nCrop(";

        var insight = AvsService().Analyze(text, text.Length, [crop]).Insight;

        Assert.NotNull(insight);
        Assert.True(insight.ImplicitClip);
    }

    [Fact]
    public void Complete_UsedNamedArgument_IsSkipped()
    {
        const string used = "clip = core.std.BlankClip()\nclip.std.Crop(left=1, ";

        var reply = VsService().Analyze(used, used.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "left=");
        Assert.Contains(reply.Items, x => x.InsertionText == "right=");
    }

    [Fact]
    public void Complete_NamedArgumentValue_SkipsName()
    {
        const string value = "clip = core.std.BlankClip()\nclip.std.Crop(left=";

        var inValue = VsService().Analyze(value, value.Length, Vs);

        Assert.DoesNotContain(inValue.Items, x => x.InsertionText == "left=");
    }

    [Fact]
    public void Complete_EmptyNativeCall_HasNoParameterNames()
    {
        const string cache = "core.clear_cache(";

        var empty = VsService().Analyze(cache, cache.Length, Vs);

        Assert.DoesNotContain(empty.Items, x => x.InsertionText == "parameters=");
    }

    [Fact]
    public void Hover_Equality_IsNotNamedArgument()
    {
        const string text = "core.std.BlankClip(width == 640)";
        var caret = text.IndexOf("width", StringComparison.Ordinal) + 1;

        var hover = VsService().Analyze(text, caret, Vs).Hover;

        Assert.True(hover == null || hover.Text != "int");
    }

    [Fact]
    public void Complete_OpenListInCall_SkipsParameterNames()
    {
        const string list = "core.std.Crop([";

        var inList = VsService().Analyze(list, list.Length, Vs);

        Assert.DoesNotContain(inList.Items, x => x.InsertionText == "clip=");
        Assert.DoesNotContain(inList.Items, x => x.InsertionText == "left=");
    }

    [Fact]
    public void Complete_GroupedNamedValue_SkipsParameterNames()
    {
        const string grouping = "core.std.Crop(left=(";

        var inValue = VsService().Analyze(grouping, grouping.Length, Vs);

        Assert.DoesNotContain(inValue.Items, x => x.InsertionText == "clip=");
        Assert.DoesNotContain(inValue.Items, x => x.InsertionText == "left=");
        Assert.DoesNotContain(inValue.Items, x => x.InsertionText == "right=");
    }

    [Fact]
    public void Insight_KeywordOnlyAfterPositional_SelectsRadius()
    {
        const string call = """
            def f(clip, *, radius=2):
                return clip
            f(x, 
            """;

        var insight = VsService().Analyze(call, call.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal("Parameter 2: radius=2", OverloadProvider.ActiveParameterText(insight));
    }

    [Fact]
    public void Complete_AviSynthUnnamedParameters_AreNotKeywords()
    {
        var avs = new[] { new Symbol("Foo", ["clip", "int"]) };

        var unnamed = AvsService().Analyze("Foo(", 4, avs);

        Assert.DoesNotContain(unnamed.Items, x => x.InsertionText == "clip=");
        Assert.DoesNotContain(unnamed.Items, x => x.InsertionText == "int=");
    }

    [Fact]
    public void Complete_PositionalClip_SkipsClipKeyword()
    {
        const string positional = "core.std.Crop(clip, ";

        var afterClip = VsService().Analyze(positional, positional.Length, Vs);

        Assert.DoesNotContain(afterClip.Items, x => x.InsertionText == "clip=");
        Assert.Contains(afterClip.Items, x => x.InsertionText == "left=");
    }

    [Fact]
    public void Complete_PositionalOnlySlash_SkipsClipKeyword()
    {
        const string positionalOnly = """
            def f(clip, /, radius=2):
                return clip
            f(
            """;

        var slash = VsService().Analyze(positionalOnly, positionalOnly.Length, Vs);

        Assert.DoesNotContain(slash.Items, x => x.InsertionText == "clip=");
        Assert.Contains(slash.Items, x => x.InsertionText == "radius=");
    }

    [Fact]
    public void Complete_InsideExpression_SkipsParameterNames()
    {
        const string expression = "core.std.Crop(x + ";

        var inside = VsService().Analyze(expression, expression.Length, Vs);

        Assert.DoesNotContain(inside.Items, x => x.InsertionText == "clip=");
        Assert.DoesNotContain(inside.Items, x => x.InsertionText == "left=");
        Assert.DoesNotContain(inside.Items, x => x.InsertionText == "right=");
    }

    [Fact]
    public void Complete_AfterStringArgument_SkipsSuppliedSlot()
    {
        const string text = """
            def f(path, radius=2):
                return path
            f("input.mkv", 
            """;

        var afterString = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(afterString.Items, x => x.InsertionText == "path=");
        Assert.Contains(afterString.Items, x => x.InsertionText == "radius=");
    }

    [Fact]
    public void Complete_AfterNestedCall_SkipsSuppliedSlot()
    {
        const string nested = """
            def f(path, radius=2):
                return path
            f(core.std.BlankClip(width=640), 
            """;

        var afterCall = VsService().Analyze(nested, nested.Length, Vs);

        Assert.DoesNotContain(afterCall.Items, x => x.InsertionText == "path=");
        Assert.Contains(afterCall.Items, x => x.InsertionText == "radius=");
    }

    [Fact]
    public void Insight_NestedDef_HasLocalCall()
    {
        const string text = """
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
    public void Complete_ExistingEquals_DoesNotInsertSecond()
    {
        const string text = """
            def f(radius=2):
                return radius
            f(radius=2)
            """;
        var caret = text.LastIndexOf("radius", StringComparison.Ordinal) + 3;

        var reply = VsService().Analyze(text, caret, Vs);

        var item = Assert.Single(reply.Items, x => x.InsertionText is "radius" or "radius=");
        Assert.Equal("radius", item.InsertionText);
    }

    [Fact]
    public void Complete_AfterExistingValue_SkipsNames()
    {
        const string text = """
            def f(path, radius=2):
                return path
            f("input.mkv"
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "path=");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "radius=");
    }

    [Fact]
    public void Insight_FunctionKeywordArgument_DoesNotRecoverDeclaration()
    {
        const string call = """
            core.std.Crop(
            function=1,
            right=
            """;

        var insight = VsService().Analyze(call, call.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal("core.std.Crop", insight.Overloads[0].Name);
        Assert.Equal(2, insight.ActiveParameter);
    }

    [Fact]
    public void Insight_FunctionCallArgument_DoesNotRecoverDeclaration()
    {
        const string invoked = """
            core.std.Crop(
            function(),
            right=
            """;

        var invokedInsight = VsService().Analyze(invoked, invoked.Length, Vs).Insight;

        Assert.NotNull(invokedInsight);
        Assert.Equal("core.std.Crop", invokedInsight.Overloads[0].Name);
    }

    [Fact]
    public void Insight_FunctionParameterNamedFunction_KeepsHelper()
    {
        const string header = """
            def helper(
                function=1,
                radius=2):
                return 1
            helper(
            """;

        var helper = VsService().Analyze(header, header.Length, Vs).Insight;

        Assert.NotNull(helper);
        Assert.Equal("helper", helper.Overloads[0].Name);
        Assert.Contains("radius=2", helper.Overloads[0].Signature);
    }

    [Fact]
    public void Insight_CommentBeforeNamedArgument_KeepsSource()
    {
        const string text = """
            core.std.Crop(
            # margins
            right=2
            """;

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal(2, insight.ActiveParameter);
    }

    [Fact]
    public void Complete_CommentBeforeArguments_OffersNames()
    {
        const string empty = "core.std.Crop(\n# comment\n";

        var names = VsService().Analyze(empty, empty.Length, Vs);

        Assert.Contains(names.Items, x => x.InsertionText == "left=");
        Assert.Contains(names.Items, x => x.InsertionText == "right=");
    }

    [Fact]
    public void Insight_AviSynthCommentBeforeNamedArgument_KeepsSource()
    {
        var avsCrop = new Symbol("Crop", ["clip", "int [left]", "int [right]"]);
        const string avs = """
            Crop(
            # margins
            right=2
            """;

        var avsInsight = AvsService().Analyze(avs, avs.Length, [avsCrop]).Insight;

        Assert.NotNull(avsInsight);
        Assert.Equal(2, avsInsight.ActiveParameter);
    }

    [Fact]
    public void Complete_AviSynthCommentBeforeArguments_OffersNames()
    {
        var avsCrop = new Symbol("Crop", ["clip", "int [left]", "int [right]"]);
        const string avsEmpty = "Crop(\n# comment\n";

        var avsNames = AvsService().Analyze(avsEmpty, avsEmpty.Length, [avsCrop]);

        Assert.Contains(avsNames.Items, x => x.InsertionText == "left=");
        Assert.Contains(avsNames.Items, x => x.InsertionText == "right=");
    }

    [Fact]
    public void Complete_KeywordParameter_OffersTrailingUnderscore()
    {
        var expr = new Symbol("core.std.Expr", ["clip:vnode", "lambda:float:opt"], ReturnType: "clip:vnode;");
        const string text = "core.std.Expr(";

        var reply = VsService().Analyze(text, text.Length, [expr]);

        Assert.Contains(reply.Items, x => x.InsertionText == "lambda_=");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "lambda=");
    }

    [Fact]
    public void Insight_TrailingUnderscoreKeyword_SelectsParameter()
    {
        var expr = new Symbol("core.std.Expr", ["clip:vnode", "lambda:float:opt"], ReturnType: "clip:vnode;");
        const string named = "core.std.Expr(lambda_=";

        var insight = VsService().Analyze(named, named.Length, [expr]).Insight;

        Assert.NotNull(insight);
        Assert.Equal(1, insight.ActiveParameter);
    }

    [Fact]
    public void Insight_FrameCopy_HasCall()
    {
        const string copy = """
            frame = core.std.BlankClip().get_frame(0)
            frame.copy(
            """;

        var copyInsight = VsService().Analyze(copy, copy.Length, Vs).Insight;

        Assert.NotNull(copyInsight);
        Assert.Equal("copy", copyInsight.Overloads[0].Name);
    }

    [Fact]
    public void Insight_FormatReplace_HasCall()
    {
        const string replace = """
            fmt = core.query_video_format(vs.YUV, vs.INTEGER, 8)
            fmt.replace(
            """;

        var replaceInsight = VsService().Analyze(replace, replace.Length, Vs).Insight;

        Assert.NotNull(replaceInsight);
        Assert.Equal("replace", replaceInsight.Overloads[0].Name);
    }

    [Fact]
    public void Insight_PythonVarargs_DoNotConsumeKeywordOnly()
    {
        const string text = """
            def f(*args, radius=2):
                return args
            f(1, 2, 
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "radius=");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "args=");
        Assert.NotNull(reply.Insight);
        var active = OverloadProvider.ActiveParameterText(reply.Insight);
        Assert.Contains("*args", active, StringComparison.Ordinal);
        Assert.DoesNotContain("No more parameters", active, StringComparison.Ordinal);
        Assert.DoesNotContain("radius=2", active, StringComparison.Ordinal);
    }

    [Fact]
    public void Insight_NamedArgumentAfterVarargs_SelectsKeyword()
    {
        const string named = """
            def f(*args, radius=2):
                return args
            f(1, radius=3
            """;

        var insight = VsService().Analyze(named, named.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal("Parameter 2: radius=2", OverloadProvider.ActiveParameterText(insight));
    }

    [Fact]
    public void Insight_KwargsNamedArgument_StaysOnKwargs()
    {
        const string kwargs = """
            def g(**kwargs):
                return kwargs
            g(radius=
            """;

        var extra = VsService().Analyze(kwargs, kwargs.Length, Vs).Insight;

        Assert.NotNull(extra);
        var text = OverloadProvider.ActiveParameterText(extra);
        Assert.Contains("**kwargs", text, StringComparison.Ordinal);
        Assert.DoesNotContain("No more parameters", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Insight_NativeTrailingUnderscore_SelectsParameter()
    {
        const string text = "core.std.Crop(source, right_=2";

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Equal(2, insight.ActiveParameter);
        Assert.Equal("right:int:opt", insight.Overloads[0].Parameters![2]);
    }

    [Fact]
    public void Complete_NativeTrailingUnderscore_SkipsUsedName()
    {
        const string text = "core.std.Crop(source, right_=2";
        const string complete = text + ", ";

        var reply = VsService().Analyze(complete, complete.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "left=");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "right=");
    }

    [Fact]
    public void Insight_PythonParameterUnderscore_SelectsIt()
    {
        const string helper = """
            def Crop(clip, right_=0):
                return clip
            Crop(source, right_=
            """;

        var python = VsService().Analyze(helper, helper.Length, Vs).Insight;

        Assert.NotNull(python);
        Assert.Contains("right_=0", OverloadProvider.ActiveParameterText(python), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("BlankClip(", "Parameter 2: int [length]")]
    [InlineData("BlankClip(100,", "Parameter 3: int [width]")]
    [InlineData("BlankClip(100, 640,", "Parameter 4: int [height]")]
    [InlineData("BlankClip(length=100,", "Parameter 3: int [width]")]
    [InlineData("BlankClip(length=100, width=160,", "Parameter 4: int [height]")]
    [InlineData("BlankClip(width=640, height=", "Parameter 4: int [height]")]
    public void Insight_AviSynthBlankClip_AdvancesAfterImplicitClip(string text, string active)
    {
        var parsed = AviSynthParameters.Parse(
            "[]c*[length]i[width]i[height]i[pixel_type]s[fps]f[fps_denominator]i[audio_rate]i[channels]i[sample_type]s[color]i[color_yuv]i[clip]c[colors]f+")!;

        var insight = AvsService().Analyze(text, text.Length, [new Symbol("BlankClip", parsed)]).Insight;

        Assert.NotNull(insight);
        Assert.Equal(active, OverloadProvider.ActiveParameterText(insight));
    }

    [Fact]
    public void Insight_DuplicateAviSynthOverloads_AreUnique()
    {
        var parsed = AviSynthParameters.Parse("c[length]i[width]i")!;
        var catalog = Enumerable.Repeat(new Symbol("BlankClip", parsed), 4).ToArray();
        const string text = "BlankClip(";

        var insight = AvsService().Analyze(text, text.Length, catalog).Insight;

        Assert.NotNull(insight);
        Assert.Single(insight.Overloads);
    }

    [Fact]
    public void Insight_AviSynthExplicitClip_MapsFirstArgument()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]", "int [top]"]);
        const string text = "src = last\nCrop(src, 10";

        var insight = AvsService().Analyze(text, text.Length, [crop]).Insight!;

        Assert.False(insight.ImplicitClip);
        Assert.Equal(1, insight.ActiveParameter);
    }

    [Fact]
    public void Insight_AviSynthDisplayedOverload_MapsParameter()
    {
        var overloads = new[]
        {
            new Symbol("F", ["clip"]),
            new Symbol("F", ["clip", "int [left]", "int [right]"])
        };
        const string text = "F(source, right=2";

        var insight = AvsService().Analyze(text, text.Length, overloads).Insight;

        Assert.NotNull(insight);
        Assert.Equal("int [right]", OverloadProvider.ActiveParameterText(insight, 1).Split(": ", 2)[^1]);
    }

    [Fact]
    public void Insight_AlreadySuppliedKeyword_IsSkipped()
    {
        const string text = """
            def f(width: int, height: int):
                return width
            f(width=1920, 
            """;

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Contains("height", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
        Assert.DoesNotContain("width: int", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_AlreadySuppliedKeyword_IsSkipped()
    {
        const string text = """
            def f(width: int, height: int):
                return width
            f(width=1920, 
            """;

        var names = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(names.Items, x => x.InsertionText == "height=");
        Assert.DoesNotContain(names.Items, x => x.InsertionText == "width=");
    }

    [Fact]
    public void Insight_PythonTrailingUnderscore_BindsKwargs()
    {
        const string text = """
            def f(radius: int=2, **kwargs):
                return radius
            f(radius_=3
            """;

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Contains("**kwargs", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
        Assert.DoesNotContain("radius: int=2", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
    }

    [Fact]
    public void Hover_PythonTrailingUnderscore_OmitsRadiusType()
    {
        const string text = """
            def f(radius: int=2, **kwargs):
                return radius
            f(radius_=3
            """;
        var hoverAt = text.LastIndexOf("radius_", StringComparison.Ordinal) + 1;

        var hover = VsService().Analyze(text, hoverAt, Vs).Hover;

        Assert.True(hover == null || !hover.Text.Contains("int=2", StringComparison.Ordinal));
    }

    [Fact]
    public void Complete_PythonTrailingUnderscore_StillOffersRadius()
    {
        const string text = """
            def f(radius: int=2, **kwargs):
                return radius
            f(radius_=3
            """;
        const string more = text + ", ";

        var names = VsService().Analyze(more, more.Length, Vs);

        Assert.Contains(names.Items, x => x.InsertionText == "radius=");
    }

    [Fact]
    public void Insight_PositionalOnlyKeyword_BindsKwargs()
    {
        const string text = """
            def f(source, /, **kwargs):
                return source
            f(clip, source=2
            """;

        var insight = VsService().Analyze(text, text.Length, Vs).Insight;

        Assert.NotNull(insight);
        Assert.Contains("**kwargs", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
        Assert.DoesNotContain("Parameter 1: source", OverloadProvider.ActiveParameterText(insight),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Insight_IndependentNewline_DoesNotCross()
    {
        const string helper = "def Filter(clip):\n    return clip\n";
        var service = VsService(Includes(Read));
        const string text = "from helper import Filter\n(";
        IncludeFile? Read(string specifier, string? _) =>
            specifier == "helper" ? new IncludeFile("/plugins/helper.py", helper) : null;

        var reply = service.Analyze(text, text.Length, Vs);

        Assert.True(reply.Insight == null || reply.Insight.Overloads.All(x => x.Name != "Filter"));
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "clip=");
    }

    [Fact]
    public void Insight_OptionalKeywordArgument_KeepsInsight()
    {
        const string optional = """
            def f(width: int, height: Optional[int] = None):
                return width
            f(1, height=
            """;

        var height = VsService().Analyze(optional, optional.Length, Vs).Insight;

        Assert.NotNull(height);
        Assert.Contains("height: Optional[int]", OverloadProvider.ActiveParameterText(height),
            StringComparison.Ordinal);
        Assert.DoesNotContain("No more parameters", OverloadProvider.ActiveParameterText(height),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Insight_QuotedKeywordArgument_KeepsInsight()
    {
        const string quoted = """
            def g(mode: "int | None" = None):
                return mode
            g(mode=
            """;

        var mode = VsService().Analyze(quoted, quoted.Length, Vs).Insight;

        Assert.NotNull(mode);
        Assert.Contains("mode:", OverloadProvider.ActiveParameterText(mode), StringComparison.Ordinal);
        Assert.DoesNotContain("No more parameters", OverloadProvider.ActiveParameterText(mode),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Insight_ArrayKeywordArgument_KeepsInsight()
    {
        var planes = new Symbol("core.std.ShufflePlanes",
            ["clip:vnode", "planes:int[]", "colorfamily:int"], ReturnType: "clip:vnode;");
        const string call = "core.std.ShufflePlanes(clip, planes=";

        var insight = VsService().Analyze(call, call.Length, [planes]).Insight;

        Assert.NotNull(insight);
        Assert.Contains("planes:int[]", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
    }

    [Fact]
    public void Hover_PositionalOnlyKeyword_BelongsToKwargs()
    {
        const string text = """
            def f(source: int, /, **kwargs):
                return source
            f(1, source=2)
            """;
        var caret = text.IndexOf("source=", StringComparison.Ordinal) + 1;

        var hover = VsService().Analyze(text, caret, Vs).Hover;

        Assert.True(hover == null || !hover.Text.Contains("int", StringComparison.Ordinal));
    }

    [Fact]
    public void Insight_UnknownKeyword_DoesNotHighlightRepeating()
    {
        var every = new Symbol("core.std.SelectEvery",
            ["clip:vnode", "cycle:int", "offsets:int[]"], ReturnType: "clip:vnode;");
        const string text = "core.std.SelectEvery(clip, mystery=";

        var insight = VsService().Analyze(text, text.Length, [every]).Insight;

        Assert.NotNull(insight);
        Assert.Equal("No more parameters", OverloadProvider.ActiveParameterText(insight));
        Assert.Equal(insight.ActiveParameter, insight.GetActiveParameter(0));
    }

    [Fact]
    public void Insight_ExtraCommasOnFixedParameters_ShowNoMore()
    {
        var crop = new CallInsight([new("core.std.Crop", ["clip:vnode", "left:int:opt", "right:int:opt"])], 4, false);

        var text = OverloadProvider.ActiveParameterText(crop);

        Assert.Equal("No more parameters", text);
    }

    [Fact]
    public void Insight_ExtraCommasOnSixParameters_ShowNoMore()
    {
        var six = new CallInsight(
            [new("core.rife.RIFE", ["clip:vnode", "a:int:opt", "b:int:opt", "c:int:opt", "d:int:opt", "e:int:opt"])], 10,
            false);

        var text = OverloadProvider.ActiveParameterText(six);

        Assert.Equal("No more parameters", text);
    }

    [Fact]
    public void Insight_ExtraCommasOnRepeatingOffsets_StayOnParameter()
    {
        var every = new CallInsight([new("core.std.SelectEvery", ["clip:vnode", "cycle:int", "offsets:int[]"])], 5, false);

        var text = OverloadProvider.ActiveParameterText(every);

        Assert.Contains("offsets:int[]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Insight_ExtraCommasOnRepeatingPlanes_StayOnParameter()
    {
        var planes = new CallInsight([new("ShufflePlanes", ["clip", "int* [planes]"])], 3, false);

        var text = OverloadProvider.ActiveParameterText(planes);

        Assert.Contains("int* [planes]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Insight_LambdaArgument_DoesNotStealFollowingParameter()
    {
        var catalog = Vs.Concat([
            new Symbol("core.std.FrameEval",
                ["clip:vnode", "eval:func", "prop_src:vnode:opt"], ReturnType: "clip:vnode;")
        ]).ToArray();
        const string text = "core.std.FrameEval(clip, lambda n, f: clip, ";

        var insight = VsService().Analyze(text, text.Length, catalog).Insight;

        Assert.NotNull(insight);
        Assert.Contains("prop_src", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
        Assert.DoesNotContain("eval:func", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_LambdaDefault_DoesNotInventParameter()
    {
        const string text = """
            def helper(callback=lambda x, y: x):
                return callback
            helper(
            """;

        var reply = VsService().Analyze(text, text.Length, Vs);

        Assert.Contains(reply.Items, x => x.InsertionText == "callback=");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "y=");
        Assert.DoesNotContain(reply.Insight!.Overloads[0].Parameters!.Select(ParameterNames.LocalName),
            x => x == "y");
    }
}
