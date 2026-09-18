using Avalonia.Controls;
using Avalonia.Media;
using HanumanInstitute.ScriptAssist.AvaloniaEdit;
using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests;

public class CompletionDataTests
{
    [Fact]
    public void Complete_FunctionHint_ShowsTruncatedParameters()
    {
        var longSignature = "core.rife.RIFE(" + string.Join(", ", Enumerable.Repeat("clip:vnode:opt", 20)) + ")";
        var item = new CompletionItem("RIFE", 0, 4, SymbolKind.Function, longSignature);

        var hint = CompletionData.HintText(item)!;

        Assert.DoesNotContain("Function", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("core.rife.RIFE", hint, StringComparison.Ordinal);
        Assert.StartsWith("clip:vnode:opt", hint, StringComparison.Ordinal);
        Assert.Contains("clip:vnode:opt, clip:vnode:opt", hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_EmptyFunctionHint_ShowsNoParameters()
    {
        var hint = CompletionData.HintText(new("Foo", 0, 3, SymbolKind.Function, "Foo()"));

        Assert.Equal("No parameters", hint);
    }

    [Fact]
    public void Complete_NamespaceHint_ReturnsNull()
    {
        var hint = CompletionData.HintText(new("rife", 0, 4, SymbolKind.Namespace, "core.rife"));

        Assert.Null(hint);
    }

    [Theory]
    [InlineData(SymbolKind.Property, "width", 5, "width: int", "int")]
    [InlineData(SymbolKind.Local, "C", 1, "C: clip", "clip")]
    [InlineData(SymbolKind.Property, "fps", 3, "fps: Fraction", "Fraction")]
    public void Complete_TypedHint_ShowsType(SymbolKind kind, string name, int length, string signature, string expected)
    {
        var hint = CompletionData.HintText(new(name, 0, length, kind, signature));

        Assert.Equal(expected, hint);
    }

    [Fact]
    public void Complete_PropertyWithoutType_ReturnsNull()
    {
        var hint = CompletionData.HintText(new("fps", 0, 3, SymbolKind.Property, "fps"));

        Assert.Null(hint);
    }

    [Fact]
    public void Complete_HintDescription_WrapsAndEllipsizes()
    {
        var longSignature = "core.rife.RIFE(" + string.Join(", ", Enumerable.Repeat("clip:vnode:opt", 20)) + ")";
        var item = new CompletionItem("RIFE", 0, 4, SymbolKind.Function, longSignature);
        var hint = CompletionData.HintText(item)!;

        var description = Assert.IsType<TextBlock>(new CompletionData(item).Description);

        Assert.Equal(CompletionData.HintMaxWidth, description.MaxWidth);
        Assert.Equal(CompletionData.HintMaxLines, description.MaxLines);
        Assert.Equal(TextWrapping.Wrap, description.TextWrapping);
        Assert.Equal(TextTrimming.CharacterEllipsis, description.TextTrimming);
        Assert.Equal(hint, description.Text);
    }

    [Fact]
    public void Complete_LongHint_TruncatesWithEllipsis()
    {
        var huge = CompletionData.TruncateHint(new('x', 500));

        Assert.True(huge.Length <= 400);
        Assert.EndsWith("…", huge);
    }

    [Fact]
    public void Complete_HoverTip_TruncatesLongText()
    {
        var text = new string('x', 500) + "(" + string.Join(", ", Enumerable.Repeat("clip:vnode:opt", 20)) + ")";

        var hover = HoverPresenter.CreateTip(text);

        Assert.Equal(CompletionData.HintMaxWidth, hover.MaxWidth);
        Assert.Equal(CompletionData.HintMaxLines, hover.MaxLines);
        Assert.Equal(TextWrapping.Wrap, hover.TextWrapping);
        Assert.Equal(TextTrimming.CharacterEllipsis, hover.TextTrimming);
        Assert.True(hover.Text!.Length <= 400);
        Assert.EndsWith("…", hover.Text);
    }
}
