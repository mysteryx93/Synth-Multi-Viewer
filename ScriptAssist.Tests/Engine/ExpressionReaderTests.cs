using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests;

[SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken")]
public class ExpressionReaderTests
{
    [Fact]
    public void Parse_TrailingCall_IncludesCallSegment()
    {
        var segments = ExpressionReader.Parse("vs.core.std.BlankClip()");

        Assert.Equal(4, segments.Count);
        Assert.Equal("vs", segments[0].Name);
        Assert.Equal(PathSegmentKind.Name, segments[0].Kind);
        Assert.Equal("BlankClip", segments[3].Name);
        Assert.Equal(PathSegmentKind.Call, segments[3].Kind);
    }

    [Fact]
    public void Parse_TrailingIndex_IncludesIndexSegment()
    {
        var segments = ExpressionReader.Parse("clip[0:10]");

        Assert.Equal("clip", Assert.Single(segments).Name);
        Assert.Equal(PathSegmentKind.Index, segments[0].Kind);
    }

    [Fact]
    public void Read_ConsecutiveCallAndIndex_WalksBoth()
    {
        const string code = "core.std.BlankClip()[0:10].";

        var path = ExpressionReader.Read(code, code.Length);

        Assert.Equal("", path.Typed);
        Assert.Equal(4, path.Segments.Count);
        Assert.Equal("BlankClip", path.Segments[2].Name);
        Assert.Equal(PathSegmentKind.Call, path.Segments[2].Kind);
        Assert.Equal(PathSegmentKind.Index, path.Segments[3].Kind);
    }

    [Fact]
    public void Parse_ConsecutiveCallAndIndex_IncludesBoth()
    {
        var segments = ExpressionReader.Parse("core.std.BlankClip()[0:10]");

        Assert.Equal("BlankClip", segments[2].Name);
        Assert.Equal(PathSegmentKind.Call, segments[2].Kind);
        Assert.Equal(PathSegmentKind.Index, segments[3].Kind);
    }

    [Fact]
    public void Read_ClosedCall_WalksThrough()
    {
        const string code = "clip.std.Crop(0, 0, 2, 2).std.";

        var path = ExpressionReader.Read(code, code.Length);

        Assert.Equal("", path.Typed);
        Assert.Equal(4, path.Segments.Count);
        Assert.Equal("Crop", path.Segments[2].Name);
        Assert.Equal(PathSegmentKind.Call, path.Segments[2].Kind);
        Assert.Equal("std", path.Segments[3].Name);
    }

    [Fact]
    public void Read_ParenthesizedReceiver_WalksName()
    {
        const string code = "(clip).";

        var path = ExpressionReader.Read(code, code.Length);

        Assert.Equal("", path.Typed);
        var segment = Assert.Single(path.Segments);
        Assert.Equal("clip", segment.Name);
        Assert.Equal(PathSegmentKind.Name, segment.Kind);
    }

    [Theory]
    [InlineData(" (cond ? a : b) ", "cond ? a : b")]
    [InlineData("((a + b))", "a + b")]
    [InlineData("(a) + (b)", "(a) + (b)")]
    [InlineData("(foo[0])", "foo[0]")]
    public void Unwrap_MatchingOuterPairs_StripsOnlyThose(string text, string expected)
    {
        var result = ExpressionParts.UnwrapParentheses(text);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Parse_ParenthesesInsideStrings_KeepsCall()
    {
        var segments = ExpressionReader.Parse("core.demo.Source(\"movie).mkv\")");

        Assert.Equal("Source", segments[^1].Name);
        Assert.Equal(PathSegmentKind.Call, segments[^1].Kind);
    }

    [Theory]
    [InlineData("not source")]
    [InlineData("source is source")]
    public void Parse_PartialExpression_ReturnsEmpty(string text)
    {
        var segments = ExpressionReader.Parse(text);

        Assert.Empty(segments);
    }

    [Fact]
    public void Parse_WholeName_ReturnsName()
    {
        var segments = ExpressionReader.Parse("source");

        Assert.Equal("source", Assert.Single(segments).Name);
    }

    [Fact]
    public void Read_NewStatement_StopsAtBoundary()
    {
        const string code = "source.\ncore.std.";

        var path = ExpressionReader.Read(code, code.Length);

        Assert.Equal("", path.Typed);
        Assert.Equal(2, path.Segments.Count);
        Assert.Equal("core", path.Segments[0].Name);
        Assert.Equal("std", path.Segments[1].Name);
    }

    [Fact]
    public void Parse_GroupedMultilineMemberChain_KeepsSegments()
    {
        var segments = ExpressionReader.Parse("""
            (core.std.BlankClip()
            .std.Crop(left=2))
            """);

        Assert.Equal("core", segments[0].Name);
        Assert.Equal("BlankClip", segments[2].Name);
        Assert.Equal(PathSegmentKind.Call, segments[2].Kind);
        Assert.Equal("Crop", segments[4].Name);
        Assert.Equal(PathSegmentKind.Call, segments[4].Kind);
    }

    [Fact]
    public void Read_RecoveredBracketMismatch_DoesNotCross()
    {
        const string code = "broken = ([0)\nsource.\ncore.std.";

        var path = ExpressionReader.Read(code, code.Length);

        Assert.Equal("core", path.Segments[0].Name);
        Assert.Equal("std", path.Segments[1].Name);
        Assert.DoesNotContain(path.Segments, x => x.Name == "source");
    }

    [Fact]
    public void Parse_LongContinuation_WalksWithoutQuadraticRescan()
    {
        var text = "(core" + string.Concat(Enumerable.Repeat("\n.std", 1000)) + ".BlankClip())";

        var segments = ExpressionReader.Parse(text);

        Assert.Equal("core", segments[0].Name);
        Assert.Equal("BlankClip", segments[^1].Name);
        Assert.Equal(PathSegmentKind.Call, segments[^1].Kind);
    }
}
