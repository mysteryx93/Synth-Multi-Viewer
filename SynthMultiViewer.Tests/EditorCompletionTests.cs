using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using HanumanInstitute.ScriptAssist;
using HanumanInstitute.ScriptAssist.AvaloniaEdit;
using HanumanInstitute.ScriptAssist.VapourSynth;
using HanumanInstitute.SynthMultiViewer.Controls;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

[SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken")]
public class EditorCompletionTests
{
    private static readonly Symbol[] Vs =
    [
        new("core.std.Crop", ["clip:vnode", "left:int:opt", "right:int:opt"], ReturnType: "clip:vnode"),
        new("core.std.BlankClip", ["width:int:opt", "height:int:opt"], ReturnType: "clip:vnode"),
        new("core.rife.RIFE", ["clip:vnode", "model:int:opt"], ReturnType: "clip:vnode"),
        new("core.std.SelectEvery", ["clip:vnode", "cycle:int", "offsets:int[]"], ReturnType: "clip:vnode")
    ];

    private static LanguageService Service() =>
        new(new VapourSynthLanguage(), new CatalogCache(() => []));

    [Fact]
    public void ExtraCommasStayOnRepeatingParameterOrStop()
    {
        var crop = new CallInsight([new("core.std.Crop", ["clip:vnode", "left:int:opt", "right:int:opt"])], 4, false);
        Assert.Equal("No more parameters", OverloadProvider.ActiveParameterText(crop));
        var six = new CallInsight(
            [new("core.rife.RIFE", ["clip:vnode", "a:int:opt", "b:int:opt", "c:int:opt", "d:int:opt", "e:int:opt"])], 10,
            false);
        Assert.Equal("No more parameters", OverloadProvider.ActiveParameterText(six));
        var every = new CallInsight([new("core.std.SelectEvery", ["clip:vnode", "cycle:int", "offsets:int[]"])], 5, false);
        Assert.Contains("offsets:int[]", OverloadProvider.ActiveParameterText(every), StringComparison.Ordinal);
        var planes = new CallInsight([new("ShufflePlanes", ["clip", "int* [planes]"])], 3, false);
        Assert.Contains("int* [planes]", OverloadProvider.ActiveParameterText(planes), StringComparison.Ordinal);
    }

    [Fact]
    public void CompletionHintShowsTruncatedParametersOnly()
    {
        var longSignature = "core.rife.RIFE(" + string.Join(", ", Enumerable.Repeat("clip:vnode:opt", 20)) + ")";
        var item = new CompletionItem("RIFE", 0, 4, SymbolKind.Function, longSignature);
        var hint = CompletionData.HintText(item)!;
        Assert.DoesNotContain("Function", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("core.rife.RIFE", hint, StringComparison.Ordinal);
        Assert.StartsWith("clip:vnode:opt", hint, StringComparison.Ordinal);
        Assert.Contains("clip:vnode:opt, clip:vnode:opt", hint, StringComparison.Ordinal);
        Assert.Equal("No parameters", CompletionData.HintText(
            new CompletionItem("Foo", 0, 3, SymbolKind.Function, "Foo()")));
        Assert.Null(CompletionData.HintText(
            new CompletionItem("rife", 0, 4, SymbolKind.Namespace, "core.rife")));
        Assert.Equal("int", CompletionData.HintText(
            new CompletionItem("width", 0, 5, SymbolKind.Property, "width: int")));
        Assert.Equal("clip", CompletionData.HintText(
            new CompletionItem("C", 0, 1, SymbolKind.Local, "C: clip")));
        Assert.Equal("Fraction", CompletionData.HintText(
            new CompletionItem("fps", 0, 3, SymbolKind.Property, "fps: Fraction")));
        Assert.Null(CompletionData.HintText(
            new CompletionItem("fps", 0, 3, SymbolKind.Property, "fps")));
        var description = Assert.IsType<TextBlock>(new CompletionData(item).Description);
        Assert.Equal(CompletionData.HintMaxWidth, description.MaxWidth);
        Assert.Equal(CompletionData.HintMaxLines, description.MaxLines);
        Assert.Equal(TextWrapping.Wrap, description.TextWrapping);
        Assert.Equal(TextTrimming.CharacterEllipsis, description.TextTrimming);
        Assert.Equal(hint, description.Text);
        var huge = CompletionData.TruncateHint(new string('x', 500));
        Assert.True(huge.Length <= 400);
        Assert.EndsWith("…", huge);
    }

    [Fact]
    public Task UnicodeReplacementAndUndoPreserveDocument() =>
        UiSession.Dispatch(() =>
    {
        var text = "# 😀\n变量 = 1\n变suffix";
        var editor = new BindableTextEditor { Text = text };
        var caret = text.IndexOf("变suffix", StringComparison.Ordinal) + 1;
        var item = Assert.Single(Service().Analyze(text, caret, []).Items, x => x.InsertionText == "变量");
        new CompletionData(item).Complete(editor.TextArea, new SimpleSegment(item.Start, item.Length), EventArgs.Empty);
        Assert.Equal("# 😀\n变量 = 1\n变量", editor.Text);
        editor.Document.UndoStack.Undo();
        Assert.Equal(text, editor.Text);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("document")]
    [InlineData("text")]
    [InlineData("caret")]
    [InlineData("editor")]
    [InlineData("escape")]
    [InlineData("kind")]
    public Task StaleReplyIsDiscardedEvenIfBackendIgnoresCancellation(string change) =>
        UiSession.Dispatch(async () =>
    {
        var service = new DelayedService();
        var editor = new BindableTextEditor { Text = "co", LanguageService = service };
        var other = new TextBox();
        using var shown = TestSupport.Show(new Window { Content = new StackPanel { Children = { editor, other } } });
        editor.TextArea.Focus();
        editor.CaretOffset = 2;
        var request = editor.RequestCompletionAsync(delay: TimeSpan.Zero);
        await service.Started.Task;
        switch (change)
        {
            case "text":
                editor.Text = "cor";
                break;
            case "document":
                editor.Document = new TextDocument("co");
                break;
            case "caret":
                editor.CaretOffset = 1;
                break;
            case "editor":
                other.Focus();
                break;
            case "escape":
                editor.DismissCompletion();
                break;
            case "kind":
                editor.ScriptKind = MediaSynthUI.ScriptKind.AviSynth;
                break;
        }
        service.Reply.SetResult(new([new("core", 0, 2, SymbolKind.Keyword, "core")], null));
        await request;
        Assert.Null(editor.DisplayedReply);
        editor.DismissCompletion();
        return true;
    }, TestContext.Current.CancellationToken);

    private static HeadlessUnitTestSession UiSession =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestApplication).Assembly);

    [Fact]
    public Task InsightAdvancesAfterCommaWhenSpaceArrivesDuringDebounce() => UiSession.Dispatch(async () =>
    {
        var editor = OpenEditor("core.std.Crop(10,");
        using var shown = TestSupport.Show(new Window { Content = editor });
        editor.TextArea.Focus();
        editor.CaretOffset = editor.Text.Length;
        var request = editor.RequestCompletionAsync(showCompletion: false, delay: TimeSpan.FromMilliseconds(80));
        editor.Document.Insert(editor.CaretOffset, " ");
        editor.CaretOffset = editor.Text.Length;
        await request;
        Assert.Equal(1, editor.DisplayedReply!.Insight!.ActiveParameter);
        editor.DismissCompletion();
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ParameterInfoFollowsCaretAndOmitsCompletionList() => UiSession.Dispatch(async () =>
    {
        var editor = OpenEditor("core.std.Crop(10, 20");
        var window = new Window { Content = editor };
        using var shown = TestSupport.Show(window);
        editor.TextArea.Focus();
        editor.CaretOffset = editor.Text.Length;
        await editor.RequestCompletionAsync(showCompletion: false, delay: TimeSpan.Zero);
        Assert.Null(editor.Completion);
        Assert.Equal(1, editor.DisplayedReply!.Insight!.ActiveParameter);
        editor.CaretOffset = editor.Text.IndexOf('(') + 1;
        await Task.Delay(120, TestContext.Current.CancellationToken);
        Assert.Null(editor.Completion);
        Assert.Equal(0, editor.DisplayedReply!.Insight!.ActiveParameter);
        var comma = editor.Text.IndexOf(',');
        editor.Document.Remove(comma, 1);
        editor.CaretOffset = comma;
        await Task.Delay(120, TestContext.Current.CancellationToken);
        Assert.Null(editor.Completion);
        Assert.Equal(0, editor.DisplayedReply!.Insight!.ActiveParameter);
        TestSupport.Press(window, Key.Space, RawInputModifiers.Control | RawInputModifiers.Shift);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Null(editor.Completion);
        Assert.NotNull(editor.DisplayedReply?.Insight);
        editor.DismissCompletion();
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CurrentReplyDisplaysPopupAndInsight() => UiSession.Dispatch(async () =>
    {
        var service = new DelayedService();
        var editor = new BindableTextEditor { Text = "core.std.Crop(co", LanguageService = service };
        using var shown = TestSupport.Show(new Window { Content = editor });
        editor.TextArea.Focus();
        editor.CaretOffset = editor.Text.Length;
        service.Reply.SetResult(Service().Analyze(editor.Text, editor.CaretOffset, Vs));
        await editor.RequestCompletionAsync(delay: TimeSpan.Zero);
        Assert.NotNull(editor.DisplayedReply);
        Assert.NotNull(editor.Completion);
        Assert.NotNull(editor.Insight);
        editor.DismissCompletion();
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task EqualsDoesNotCommitCompletion() => UiSession.Dispatch(async () =>
    {
        var editor = OpenEditor("core.std.Cr");
        var window = new Window { Content = editor };
        using var shown = TestSupport.Show(window);
        editor.TextArea.Focus();
        editor.CaretOffset = editor.Text.Length;
        await editor.RequestCompletionAsync(delay: TimeSpan.Zero);
        Assert.NotNull(editor.Completion);
        window.KeyTextInput("=");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("core.std.Cr=", editor.Text);
        editor.DismissCompletion();
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task MemberDotShowsListInsteadOfOverlappingParameterInfo() => UiSession.Dispatch(async () =>
    {
        var editor = OpenEditor("core.std.Crop(core.rife.");
        using var shown = TestSupport.Show(new Window { Content = editor });
        editor.TextArea.Focus();
        editor.CaretOffset = editor.Text.Length;
        await editor.RequestCompletionAsync(delay: TimeSpan.Zero);
        Assert.NotNull(editor.Completion);
        Assert.Contains(editor.DisplayedReply!.Items, x => x.InsertionText == "RIFE");
        Assert.Null(editor.Insight);
        editor.DismissCompletion();
        await editor.RequestCompletionAsync(showCompletion: false, delay: TimeSpan.Zero);
        Assert.Null(editor.Completion);
        Assert.NotNull(editor.Insight);
        editor.DismissCompletion();
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CompletionAcceptsWithTabAndTypingOpensInsight() => UiSession.Dispatch(async () =>
    {
        var editor = OpenEditor("core.std.Cr");
        var window = new Window { Content = editor };
        using var shown = TestSupport.Show(window);
        editor.TextArea.Focus();
        editor.CaretOffset = editor.Text.Length;
        await editor.RequestCompletionAsync(delay: TimeSpan.Zero);
        TestSupport.Press(window, Key.Tab);
        Assert.Equal("core.std.Crop", editor.Text);
        window.KeyTextInput("(");
        await Task.Delay(250, TestContext.Current.CancellationToken);
        Assert.NotNull(editor.DisplayedReply?.Insight);
        Assert.Null(editor.Completion);
        TestSupport.Press(window, Key.Escape);
        Assert.Null(editor.DisplayedReply);
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CompletionInsertsWhenItemIsChosenAfterEditorLosesFocus() => UiSession.Dispatch(async () =>
    {
        var editor = OpenEditor("core.std.Cr");
        var window = new Window { Content = editor, Width = 640, Height = 320 };
        using var shown = TestSupport.Show(window);
        editor.TextArea.Focus();
        editor.CaretOffset = editor.Text.Length;
        await editor.RequestCompletionAsync(delay: TimeSpan.Zero);
        Dispatcher.UIThread.RunJobs();
        var completion = editor.Completion;
        Assert.NotNull(completion);
        editor.RaiseEvent(new RoutedEventArgs(InputElement.LostFocusEvent));
        completion.CompletionList.RequestInsertion(EventArgs.Empty);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("core.std.Crop", editor.Text);
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ClickingCompletionListInsertsText() => UiSession.Dispatch(async () =>
    {
        var editor = OpenEditor("core.std.Cr");
        var window = new Window { Content = editor, Width = 640, Height = 320 };
        using var shown = TestSupport.Show(window);
        editor.TextArea.Focus();
        editor.CaretOffset = editor.Text.Length;
        await editor.RequestCompletionAsync(delay: TimeSpan.Zero);
        Dispatcher.UIThread.RunJobs();
        var list = editor.Completion?.CompletionList.ListBox;
        Assert.NotNull(list);
        var item = list.ContainerFromIndex(Math.Max(0, list.SelectedIndex));
        Assert.NotNull(item);
        var root = TopLevel.GetTopLevel(item)!;
        var point = item.TranslatePoint(new Point(12, Math.Max(1, item.Bounds.Height / 2)), root)!.Value;
        root.MouseDown(point, MouseButton.Left);
        root.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("core.std.Crop", editor.Text);
        return true;
    }, TestContext.Current.CancellationToken);

    private static BindableTextEditor OpenEditor(string text) => new()
    {
        Text = text,
        LanguageService = new LanguageService(new VapourSynthLanguage(), new CatalogCache(() => Vs))
    };

    private sealed class DelayedService : ILanguageService
    {
        public TaskCompletionSource<bool> Started { get; } = new();
        public TaskCompletionSource<Reply> Reply { get; } = new();

        public Task<Reply> GetAsync(string text, int caret, CancellationToken cancellationToken,
            string? documentPath = null)
        {
            Started.TrySetResult(true);
            return Reply.Task;
        }

        public void Invalidate()
        {
        }
    }
}
