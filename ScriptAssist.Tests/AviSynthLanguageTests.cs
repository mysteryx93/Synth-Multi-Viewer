using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests;

using static AssistHarness;

[SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken")]
public class AviSynthLanguageTests
{
    [Fact]
    public void Analyze_GlobalAssignment_Completes()
    {
        const string text = "global foo = last\nfo";

        var reply = AvsService().Analyze(text, text.Length, []);

        Assert.Contains(reply.Items, x => x.InsertionText == "foo");
    }

    [Fact]
    public void Hover_AviSynthHeaderTypeWords_AreSilent()
    {
        var conversion = new Symbol("String", ["val"]);
        var clip = new Symbol("Clip", ["clip"]);
        const string text = """
            function Foo(clip C, string "Preset") {
                return C
            }
            """;

        var stringHover = AvsService().Analyze(text, text.IndexOf("string", StringComparison.Ordinal) + 1,
            [conversion, clip]).Hover;
        var clipType = AvsService().Analyze(text, text.IndexOf("clip", StringComparison.Ordinal) + 1, [conversion, clip])
            .Hover;
        var parameter = AvsService().Analyze(text, text.IndexOf("C,", StringComparison.Ordinal), [conversion, clip]).Hover;

        Assert.Null(stringHover);
        Assert.Null(clipType);
        Assert.Null(parameter);
    }

    [Fact]
    public void Hover_AviSynthConversionCall_ShowsSignature()
    {
        var conversion = new Symbol("String", ["val"]);
        const string text = "n = String(5)";

        var hover = AvsService().Analyze(text, text.IndexOf("String", StringComparison.Ordinal) + 1, [conversion]).Hover;

        Assert.NotNull(hover);
        Assert.Contains("String(", hover.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Hover_Property_ShowsInternal()
    {
        var height = new Symbol("Height", ["clip"]);
        const string text = "last.Height";

        var hover = AvsService().Analyze(text, text.Length, [height]).Hover;

        Assert.NotNull(hover);
        Assert.Contains("Height", hover.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_Last_OffersClipTakingFilters()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]", "int [top]"]);
        var blank = new Symbol("BlankClip", ["int [width]"]);
        const string text = "last.";

        var reply = AvsService().Analyze(text, text.Length, [crop, blank]);

        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "BlankClip");
    }

    [Fact]
    public void Analyze_UnknownIdentifierDot_IsSilent()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);

        var reply = AvsService().Analyze("foo.", 4, [crop]);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Analyze_FunctionParameter_OffersClipFilters()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        var width = new Symbol("Width", ["clip"]);
        const string text = """
            function Foo(clip C, int "amount") {
                C.
            """;

        var reply = AvsService().Analyze(text, text.Length, [crop, width]);

        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Hover_FunctionParameter_ShowsClip()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        var width = new Symbol("Width", ["clip"]);
        const string text = """
            function Foo(clip C, int "amount") {
                C.
            """;
        var hoverAt = text.LastIndexOf('C');

        var hover = AvsService().Analyze(text, hoverAt, [crop, width]).Hover;

        Assert.NotNull(hover);
        Assert.Equal("clip", hover.Text);
    }

    [Fact]
    public void Analyze_AssignedWidthCall_DoesNotOfferClipFilters()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        var width = new Symbol("Width", ["clip"]);
        const string text = """
            function Foo(clip C) {
                nw = C.Width()
                nw.
            """;

        var reply = AvsService().Analyze(text, text.Length, [crop, width]);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Analyze_ClosedFunctionParameter_DoesNotLeak()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        const string text = """
            function Foo(clip C) {
                inner = C
                return inner
            }
            C.
            """;

        var reply = AvsService().Analyze(text, text.Length, [crop]);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Analyze_ClosedFunctionLocal_DoesNotLeak()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        const string text = """
            function Foo(clip C) {
                inner = C
                return inner
            }
            inner.
            """;

        var reply = AvsService().Analyze(text, text.Length, [crop]);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Analyze_ScriptName_VisibleInsideFunction()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        const string text = """
            src = last
            function Foo(clip C) {
                src.
            """;

        var reply = AvsService().Analyze(text, text.Length, [crop]);

        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Analyze_GlobalInsideFunction_LiftsClip()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        const string text = """
            function Foo(clip C) {
                global g = C
            }
            g.
            """;

        var reply = AvsService().Analyze(text, text.Length, [crop]);

        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Analyze_TrailingHeaderContinuation_KeepsClip()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        var width = new Symbol("Width", ["clip"]);
        const string text = """
            function Foo(clip clp, \
                int "n")
            {
                clp = Default(clp, last)
                clp.
            """;

        var reply = AvsService().Analyze(text, text.Length, [crop, width]);

        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Analyze_LeadingHeaderContinuation_KeepsClip()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        const string text = """
            function Foo(clip C,
            \ int "n")
            {
                C.
            """;

        var reply = AvsService().Analyze(text, text.Length, [crop]);

        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Analyze_AssignedWidthProperty_DoesNotOfferClipFilters()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        var width = new Symbol("Width", ["clip"]);
        const string text = """
            function Foo(clip C) {
                n = C.Width
                n.
            """;

        var reply = AvsService().Analyze(text, text.Length, [crop, width]);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Insight_FunctionHeader_IsSilent()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        var foo = new Symbol("Foo", ["clip C", "int n"]);
        const string text = "function Foo(clip C,";

        var insight = AvsService().Analyze(text, text.Length, [foo, crop]).Insight;

        Assert.Null(insight);
    }

    [Fact]
    public void Analyze_HeaderWithoutBrace_DoesNotCaptureNextFunction()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        const string text = """
            function Foo(clip C)
            function Bar(clip D) {
                D.
            """;

        var reply = AvsService().Analyze(text, text.Length, [crop]);

        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Analyze_HeaderWithoutBrace_DoesNotLeakParameter()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        const string text = """
            function Foo(clip C)
            function Bar(clip D) {
                return D
            }
            C.
            """;

        var reply = AvsService().Analyze(text, text.Length, [crop]);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Analyze_ContinuedAssignment_KeepsClipType()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]", "int [top]"]);
        const string text = """
            src = last.Crop(0, 0, \
            2, 2)
            src.
            """;

        var reply = AvsService().Analyze(text, text.Length, [crop]);

        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Analyze_TernaryAssignment_KeepsClipType()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]", "int [top]"]);
        const string text = "src = true ? last : last.Crop(0, 0, 2, 2)\nsrc.";

        var reply = AvsService().Analyze(text, text.Length, [crop]);

        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Analyze_ParenthesizedTernary_KeepsClipType()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]", "int [top]"]);
        const string text = "src = (FrameDouble ? Interleave(last, last) : last)\nsrc.";

        var reply = AvsService().Analyze(text, text.Length, [crop]);

        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Analyze_StarBracketCommentAndTripleQuote_StayClip()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]", "int [top]"]);
        const string text = """"
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
        var hoverAt = text.LastIndexOf("R.", StringComparison.Ordinal);

        var reply = AvsService().Analyze(text, text.Length, [crop]);
        var hover = AvsService().Analyze(text, hoverAt, [crop]).Hover;

        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");
        Assert.NotNull(hover);
        Assert.Equal("clip", hover.Text);
    }

    [Fact]
    public void Analyze_Internals_DoNotTypeAsClip()
    {
        var width = new Symbol("Width", ["clip"]);
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        const string text = "n = Width()\nn.";

        var reply = AvsService().Analyze(text, text.Length, [width, crop]);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Analyze_BufferFunctions_AreLexedWithoutLoadingPlugins()
    {
        const string text = """
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
    }

    [Fact]
    public void Insight_BufferFunction_ShowsImplicitClip()
    {
        const string text = """
            # function Fake() {}
            /* function Fake2() {} */
            LoadPlugin("only-in-buffer.dll")
            function LocalFilter(
                clip c,
                int "amount" # argument comment
                ) { return c }
            last.LocalFilter(
            """;

        var insight = AvsService().Analyze(text, text.Length, []).Insight!;

        Assert.True(insight.ImplicitClip);
        Assert.Contains("amount", insight.Overloads[0].Signature);
    }

    [Fact]
    public void Analyze_AssignmentAfterString_StillBindsClip()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        var blank = new Symbol("BlankClip", ["int [width]"]);
        const string text = """
            path = "movie.mkv"
            clip = BlankClip()
            clip.
            """;

        var reply = AvsService().Analyze(text, text.Length, [crop, blank]);

        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Analyze_AssignmentAfterUnfinishedString_StillBindsClip()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        var blank = new Symbol("BlankClip", ["int [width]"]);
        const string text = """
            path =
            clip = BlankClip()
            clip.
            """;

        var reply = AvsService().Analyze(text, text.Length, [crop, blank]);

        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Insight_UnclosedAviSynthHeader_DoesNotHideLaterCall()
    {
        const string text = """
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
    public void Insight_UnclosedUppercaseHeader_DoesNotHideLaterCall()
    {
        const string text = """
            function Broken(clip c,
            FUNCTION Good(clip c) {
                return c
            }
            Good(
            """;

        var insight = AvsService().Analyze(text, text.Length, []).Insight;

        Assert.NotNull(insight);
        Assert.Equal("Good", insight.Overloads[0].Name);
    }

    [Fact]
    public void Hover_AviSynthBareParameter_PrefersLocalType()
    {
        var width = new Symbol("Width", ["clip"]);
        const string text = """
            function F(int width) {
                return width
            }
            """;

        var hover = AvsService().Analyze(text, text.LastIndexOf("width", StringComparison.OrdinalIgnoreCase) + 1,
            [width]).Hover;

        Assert.NotNull(hover);
        Assert.Equal("int", hover.Text);
    }

    [Fact]
    public void Hover_AviSynthWidthCall_ShowsInternalSignature()
    {
        var width = new Symbol("Width", ["clip"]);
        const string text = """
            function F(int width) {
                n = Width()
            }
            """;

        var hover = AvsService().Analyze(text, text.IndexOf("Width()", StringComparison.Ordinal) + 1, [width]).Hover;

        Assert.NotNull(hover);
        Assert.Equal(width.Signature, hover.Text);
    }

    [Fact]
    public void Analyze_AssignmentAfterOpeningBrace_Binds()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        const string text = """
            function F(clip c) { source = c
            source.
            """;

        var reply = AvsService().Analyze(text, text.Length, [crop]);

        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Analyze_SameLineClosingBrace_DoesNotCaptureAssignment()
    {
        var crop = new Symbol("Crop", ["clip", "int [left]"]);
        const string text = """
            function F(clip c) { global source = c }
            source.
            """;

        var reply = AvsService().Analyze(text, text.Length, [crop]);

        Assert.Contains(reply.Items, x => x.InsertionText == "Crop");
    }
}
