using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HanumanInstitute.MediaSynthUI;
using HanumanInstitute.SynthMultiViewer.Controls;
using HanumanInstitute.SynthMultiViewer.Helpers;
using HanumanInstitute.SynthMultiViewer.ViewModels;
using HanumanInstitute.SynthMultiViewer.Views;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class EditorBindingTests
{
    private static HeadlessUnitTestSession UiSession =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestApplication).Assembly);

    [Fact]
    public Task ScriptText_DocumentEdited_UpdatesViewModel() => UiSession.Dispatch(() =>
    {
        var model = new EditorViewModel { Script = "original" };
        var view = new EditorView { DataContext = model };
        using var window = TestSupport.Show(new Window { Content = view });
        var editor = view.FindControl<BindableTextEditor>("Editor")!;

        editor.Document.Insert(editor.Document.TextLength, " edit");

        Assert.Equal("original edit", model.Script);
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ScriptText_EditUndone_RestoresViewModelText() => UiSession.Dispatch(() =>
    {
        var model = new EditorViewModel { Script = "original" };
        var view = new EditorView { DataContext = model };
        using var window = TestSupport.Show(new Window { Content = view });
        var editor = view.FindControl<BindableTextEditor>("Editor")!;
        editor.Document.Insert(editor.Document.TextLength, " edit");

        editor.Undo();

        Assert.Equal("original", editor.Text);
        Assert.Equal("original", model.Script);
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ScriptText_DataContextReplaced_StopsUpdatingPreviousModel() => UiSession.Dispatch(() =>
    {
        var first = new EditorViewModel { Script = "first" };
        var second = new EditorViewModel { Script = "second" };
        var view = new EditorView { DataContext = first };
        using var window = TestSupport.Show(new Window { Content = view });
        var editor = view.FindControl<BindableTextEditor>("Editor")!;

        view.DataContext = second;
        first.Script = "detached";
        editor.Document.Insert(editor.Document.TextLength, " edit");

        Assert.Equal("second edit", editor.Text);
        Assert.Equal("second edit", second.Script);
        Assert.Equal("detached", first.Script);
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ScriptText_ViewModelChanges_UpdatesDocument() => UiSession.Dispatch(() =>
    {
        var model = new EditorViewModel { Script = "original" };
        var view = new EditorView { DataContext = model };
        using var window = TestSupport.Show(new Window { Content = view });
        var editor = view.FindControl<BindableTextEditor>("Editor")!;

        model.Script = "replacement";

        Assert.Equal("replacement", editor.Text);
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task HighlightSource_KindAviSynth_LoadsAviSynthDefinition() => UiSession.Dispatch(() =>
    {
        var model = new EditorViewModel { Kind = ScriptKind.AviSynth, Script = "BlankClip()" };
        var view = new EditorView { DataContext = model };
        using var window = TestSupport.Show(new Window { Content = view });
        var editor = view.FindControl<BindableTextEditor>("Editor")!;

        Assert.Equal("AviSynth.xshd", SyntaxHighlight.GetSource(editor));
        Assert.NotNull(editor.SyntaxHighlighting);
        Assert.Equal("AviSynth", editor.SyntaxHighlighting.Name);
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task HighlightSource_KindVapourSynth_LoadsPythonDefinition() => UiSession.Dispatch(() =>
    {
        var model = new EditorViewModel { Kind = ScriptKind.VapourSynth, Script = "clip = core.std.BlankClip()" };
        var view = new EditorView { DataContext = model };
        using var window = TestSupport.Show(new Window { Content = view });
        var editor = view.FindControl<BindableTextEditor>("Editor")!;

        Assert.Equal("Python.xshd", SyntaxHighlight.GetSource(editor));
        Assert.NotNull(editor.SyntaxHighlighting);
        Assert.Equal("Python", editor.SyntaxHighlighting.Name);
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task HorizontalScrollbar_PageChange_MatchesViewportWidth() => UiSession.Dispatch(() =>
    {
        var model = new EditorViewModel { Script = new string('x', 400) };
        var view = new EditorView { DataContext = model };
        using var window = TestSupport.Show(new Window { Content = view, Width = 280, Height = 200 });
        var editor = view.FindControl<BindableTextEditor>("Editor")!;
        Dispatcher.UIThread.RunJobs();

        var scroll = editor.GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert.True(scroll.Viewport.Width > 20);
        var bar = scroll.GetVisualDescendants().OfType<ScrollBar>()
            .Single(x => x.Orientation == Orientation.Horizontal);
        Assert.Equal(scroll.Viewport.Width, bar.LargeChange);
        Assert.True(bar.LargeChange > 20);
        return true;
    }, TestContext.Current.CancellationToken);
}
