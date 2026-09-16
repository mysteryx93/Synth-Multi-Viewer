using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using HanumanInstitute.SynthMultiViewer.Controls;
using HanumanInstitute.SynthMultiViewer.Services.Completion;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

[SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken")]
public class EditorCompletionTests
{
    private static readonly FilterSymbol[] Vs =
    [
        new("core.std.Crop", ["clip:vnode", "left:int:opt", "right:int:opt"]),
        new("core.std.BlankClip", ["width:int:opt", "height:int:opt"]),
        new("core.rife.RIFE", ["clip:vnode", "model:int:opt"]),
        new("core.std.SelectEvery", ["clip:vnode", "cycle:int", "offsets:int[]"])
    ];
    private static EditorLanguageService Service(bool avs = false) => new(avs, new CatalogCache(() => []));

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
        var reply = Service().Analyze(text, text.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == expected);
    }

    [Fact]
    public void VapourSynthClipAssignmentIsNotACoreAlias()
    {
        var text = "clip = vs.core.std.BlankClip()\nclip.";
        var reply = Service().Analyze(text, text.Length, Vs);
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "std");
    }

    [Fact]
    public void VapourSynthAliasedCallShowsInsight()
    {
        var text = "c = vs.core\nc.std.Crop(";
        var insight = Service().Analyze(text, text.Length, Vs).Insight!;
        Assert.Equal("core.std.Crop", insight.Overloads[0].Name);
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
        var reply = Service().Analyze(text, text.Length, Vs);
        Assert.Contains(reply.Items, x => x.InsertionText == expected);
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width");
    }

    [Fact]
    public void AviSynthGlobalAssignmentCompletes()
    {
        var text = "global foo = last\nfo";
        var reply = Service(true).Analyze(text, text.Length, []);
        Assert.Contains(reply.Items, x => x.InsertionText == "foo");
    }

    [Theory]
    [InlineData("# core.std.", false)]
    [InlineData("\"core.std.", false)]
    [InlineData("'''core.std.\n", false)]
    [InlineData("/* Crop(\n", true)]
    [InlineData("/[ Crop(\n", true)]
    public void CommentsAndStringsSuppressCompletion(string text, bool avs)
    {
        var result = Service(avs).Analyze(text, text.Length, Vs);
        Assert.Empty(result.Items);
        Assert.Null(result.Insight);
    }

    [Fact]
    public void ExtraCommasStayOnRepeatingParameterOrStop()
    {
        var crop = new CallInsight([new("core.std.Crop", ["clip:vnode", "left:int:opt", "right:int:opt"])], 4, false);
        Assert.Equal("No more parameters", EditorOverloadProvider.ActiveParameterText(crop));
        var six = new CallInsight(
            [new("core.rife.RIFE", ["clip:vnode", "a:int:opt", "b:int:opt", "c:int:opt", "d:int:opt", "e:int:opt"])], 10,
            false);
        Assert.Equal("No more parameters", EditorOverloadProvider.ActiveParameterText(six));
        var every = new CallInsight([new("core.std.SelectEvery", ["clip:vnode", "cycle:int", "offsets:int[]"])], 5, false);
        Assert.Contains("offsets:int[]", EditorOverloadProvider.ActiveParameterText(every), StringComparison.Ordinal);
        var planes = new CallInsight([new("ShufflePlanes", ["clip", "int* [planes]"])], 3, false);
        Assert.Contains("int* [planes]", EditorOverloadProvider.ActiveParameterText(planes), StringComparison.Ordinal);
    }

    [Fact]
    public void CompletionHintShowsTruncatedParametersOnly()
    {
        var longSignature = "core.rife.RIFE(" + string.Join(", ", Enumerable.Repeat("clip:vnode:opt", 20)) + ")";
        var item = new EditorCompletion("RIFE", 0, 4, CompletionKind.Function, longSignature);
        var hint = EditorCompletionData.HintText(item)!;
        Assert.DoesNotContain("Function", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("core.rife.RIFE", hint, StringComparison.Ordinal);
        Assert.StartsWith("clip:vnode:opt", hint, StringComparison.Ordinal);
        Assert.Contains("clip:vnode:opt, clip:vnode:opt", hint, StringComparison.Ordinal);
        Assert.Equal("No parameters", EditorCompletionData.HintText(
            new EditorCompletion("Foo", 0, 3, CompletionKind.Function, "Foo()")));
        Assert.Null(EditorCompletionData.HintText(
            new EditorCompletion("rife", 0, 4, CompletionKind.Namespace, "core.rife")));
        var description = Assert.IsType<TextBlock>(new EditorCompletionData(item).Description);
        Assert.Equal(EditorCompletionData.HintMaxWidth, description.MaxWidth);
        Assert.Equal(EditorCompletionData.HintMaxLines, description.MaxLines);
        Assert.Equal(TextWrapping.Wrap, description.TextWrapping);
        Assert.Equal(TextTrimming.CharacterEllipsis, description.TextTrimming);
        Assert.Equal(hint, description.Text);
        var huge = EditorCompletionData.TruncateHint(new string('x', 500));
        Assert.True(huge.Length <= 400);
        Assert.EndsWith("…", huge);
    }

    [Fact]
    public void NestedInsightTracksInnerThenOuterAndIgnoresStringCommas()
    {
        var text = "core.std.Crop(core.std.BlankClip(10, ";
        var inner = Service().Analyze(text, text.Length, Vs).Insight!;
        Assert.Equal("core.std.BlankClip", inner.Overloads[0].Name);
        Assert.Equal(1, inner.ActiveParameter);
        text += "20), 'a,b', ";
        var outer = Service().Analyze(text, text.Length, Vs).Insight!;
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
        var crop = new FilterSymbol("Crop", ["clip", "int [left]", "int [top]", "int [right]", "int [bottom]"]);
        var insight = Service(true).Analyze(text, text.Length, [crop]).Insight!;
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
        var result = Service(true).Analyze(text, text.Length, []);
        var item = Assert.Single(result.Items);
        Assert.Equal("LocalFilter", item.InsertionText);
        Assert.Contains("amount", item.Signature);
        Assert.DoesNotContain("argument comment", item.Signature);
        text = text[..^6] + "LocalFilter(";
        var insight = Service(true).Analyze(text, text.Length, []).Insight!;
        Assert.True(insight.ImplicitClip);
        Assert.Contains("amount", new EditorOverloadProvider(insight).CurrentContent.ToString());
    }

    [Theory]
    [InlineData("ci[left]i[top]i", "clip,int,int [left],int [top]")]
    [InlineData("c[planes]i*", "clip,int* [planes]")]
    [InlineData("c[items]a", "clip,array [items]")]
    [InlineData("", "")]
    public void AviSynthParameters(string format, string expected) =>
        Assert.Equal(expected, string.Join(",", EditorCatalogs.ParseAvsParameters(format)!));

    [Fact]
    public void UnknownParametersStayUnknown()
    {
        Assert.Null(EditorCatalogs.ParseAvsParameters(null));
        Assert.Null(EditorCatalogs.ParseAvsParameters("c[broken"));
        Assert.Null(EditorCatalogs.ParseAvsParameters("z"));
    }

    [Fact]
    public async Task EnumerationFailureIsCachedUntilPathChangeOrRefresh()
    {
        var count = 0;
        var cache = new CatalogCache(() => { Interlocked.Increment(ref count); throw new InvalidOperationException(); });
        cache.Refresh("path1");
        var service = new EditorLanguageService(false, cache);
        for (var i = 0; i < 5; i++)
        {
            Assert.Contains((await service.GetAsync("im", 2, CancellationToken.None)).Items, x => x.InsertionText == "import");
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
    public Task UnicodeReplacementAndUndoPreserveDocument() =>
        UiSession.Dispatch(() =>
    {
        var text = "# 😀\n变量 = 1\n变suffix";
        var editor = new BindableTextEditor { Text = text };
        var caret = text.IndexOf("变suffix", StringComparison.Ordinal) + 1;
        var item = Assert.Single(Service().Analyze(text, caret, []).Items, x => x.InsertionText == "变量");
        new EditorCompletionData(item).Complete(editor.TextArea, new SimpleSegment(item.Start, item.Length), EventArgs.Empty);
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
        service.Reply.SetResult(new([new("core", 0, 2, CompletionKind.Keyword, "core")], null));
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
        Assert.Null(editor.Insight);
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
        LanguageService = new EditorLanguageService(false, new CatalogCache(() => Vs))
    };

    private sealed class DelayedService : IEditorLanguageService
    {
        public TaskCompletionSource<bool> Started { get; } = new();
        public TaskCompletionSource<EditorReply> Reply { get; } = new();

        public Task<EditorReply> GetAsync(string text, int caret, CancellationToken cancellationToken)
        {
            Started.TrySetResult(true);
            return Reply.Task;
        }
    }
}
