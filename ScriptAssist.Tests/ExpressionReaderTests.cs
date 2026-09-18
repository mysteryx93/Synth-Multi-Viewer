using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests;

public class ExpressionReaderTests
{
    [Fact]
    public void ParseIncludesTrailingCall()
    {
        var segments = ExpressionReader.Parse("vs.core.std.BlankClip()");
        Assert.Equal(4, segments.Count);
        Assert.Equal("vs", segments[0].Name);
        Assert.Equal(PathSegmentKind.Name, segments[0].Kind);
        Assert.Equal("BlankClip", segments[3].Name);
        Assert.Equal(PathSegmentKind.Call, segments[3].Kind);
    }

    [Fact]
    public void ParseIncludesTrailingIndex()
    {
        var segments = ExpressionReader.Parse("clip[0:10]");
        Assert.Equal("clip", Assert.Single(segments).Name);
        Assert.Equal(PathSegmentKind.Index, segments[0].Kind);
    }

    [Fact]
    public void ReadWalksConsecutiveCallAndIndex()
    {
        var code = "core.std.BlankClip()[0:10].";
        var path = ExpressionReader.Read(code, code.Length);
        Assert.Equal("", path.Typed);
        Assert.Equal(4, path.Segments.Count);
        Assert.Equal("BlankClip", path.Segments[2].Name);
        Assert.Equal(PathSegmentKind.Call, path.Segments[2].Kind);
        Assert.Equal(PathSegmentKind.Index, path.Segments[3].Kind);
    }

    [Fact]
    public void ParseIncludesConsecutiveCallAndIndex()
    {
        var segments = ExpressionReader.Parse("core.std.BlankClip()[0:10]");
        Assert.Equal("BlankClip", segments[2].Name);
        Assert.Equal(PathSegmentKind.Call, segments[2].Kind);
        Assert.Equal(PathSegmentKind.Index, segments[3].Kind);
    }

    [Fact]
    public void ReadWalksThroughClosedCall()
    {
        var code = "clip.std.Crop(0, 0, 2, 2).std.";
        var path = ExpressionReader.Read(code, code.Length);
        Assert.Equal("", path.Typed);
        Assert.Equal(4, path.Segments.Count);
        Assert.Equal("Crop", path.Segments[2].Name);
        Assert.Equal(PathSegmentKind.Call, path.Segments[2].Kind);
        Assert.Equal("std", path.Segments[3].Name);
    }

    [Fact]
    public void ReadWalksParenthesizedReceiver()
    {
        var code = "(clip).";
        var path = ExpressionReader.Read(code, code.Length);
        Assert.Equal("", path.Typed);
        var segment = Assert.Single(path.Segments);
        Assert.Equal("clip", segment.Name);
        Assert.Equal(PathSegmentKind.Name, segment.Kind);
    }

    [Fact]
    public void UnwrapParenthesesStripsMatchingOuterPairsOnly()
    {
        Assert.Equal("cond ? a : b", ExpressionParts.UnwrapParentheses(" (cond ? a : b) "));
        Assert.Equal("a + b", ExpressionParts.UnwrapParentheses("((a + b))"));
        Assert.Equal("(a) + (b)", ExpressionParts.UnwrapParentheses("(a) + (b)"));
        Assert.Equal("foo[0]", ExpressionParts.UnwrapParentheses("(foo[0])"));
    }

    [Fact]
    public void ParseIgnoresParenthesesInsideStrings()
    {
        var segments = ExpressionReader.Parse("core.demo.Source(\"movie).mkv\")");
        Assert.Equal("Source", segments[^1].Name);
        Assert.Equal(PathSegmentKind.Call, segments[^1].Kind);
    }

    [Fact]
    public void ParseRequiresTheWholeExpression()
    {
        Assert.Empty(ExpressionReader.Parse("not source"));
        Assert.Empty(ExpressionReader.Parse("source is source"));
        Assert.Equal("source", Assert.Single(ExpressionReader.Parse("source")).Name);
    }

    [Fact]
    public void ReadStopsAtANewStatement()
    {
        var code = "source.\ncore.std.";
        var path = ExpressionReader.Read(code, code.Length);
        Assert.Equal("", path.Typed);
        Assert.Equal(2, path.Segments.Count);
        Assert.Equal("core", path.Segments[0].Name);
        Assert.Equal("std", path.Segments[1].Name);
    }
}
