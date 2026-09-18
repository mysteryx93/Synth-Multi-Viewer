using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Media;
using HanumanInstitute.MediaSynthUI;
using HanumanInstitute.SynthMultiViewer.Controls;
using HanumanInstitute.SynthMultiViewer.Helpers;
using HanumanInstitute.SynthMultiViewer.ViewModels;
using HanumanInstitute.SynthMultiViewer.Views;
using ReactiveUI;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class BehaviorTests
{
    [AvaloniaFact]
    public async Task HeaderEditDone_EnterPressed_CommitsBoundName()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        Dispatcher.UIThread.RunJobs();
        ((ICommand)model.Rename).Execute(null);
        Dispatcher.UIThread.RunJobs();

        view.KeyTextInput("Renamed tab");
        TestSupport.Press(view, Key.Enter);

        Assert.Equal("Renamed tab", model.SelectedItem!.DisplayName);
        Assert.False(model.SelectedItem.IsEditingHeader);
    }

    [AvaloniaFact]
    public async Task HeaderEditCancel_EscapePressed_RestoresName()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        Dispatcher.UIThread.RunJobs();
        var original = model.SelectedItem!.DisplayName;
        ((ICommand)model.Rename).Execute(null);
        Dispatcher.UIThread.RunJobs();

        view.KeyTextInput("Renamed tab");
        TestSupport.Press(view, Key.Escape);

        Assert.Equal(original, model.SelectedItem.DisplayName);
        Assert.False(model.SelectedItem.IsEditingHeader);
    }

    [AvaloniaFact]
    public async Task HeaderEditDone_BlankName_RestoresName()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        Dispatcher.UIThread.RunJobs();
        var original = model.SelectedItem!.DisplayName;
        ((ICommand)model.Rename).Execute(null);
        Dispatcher.UIThread.RunJobs();
        var box = view.GetVisualDescendants().OfType<TextBox>()
            .Single(x => x.Classes.Contains("tab-header-edit"));

        box.Text = "";
        TestSupport.Press(view, Key.Enter);

        Assert.False(model.SelectedItem.IsEditingHeader);
        Assert.Equal(original, model.SelectedItem.DisplayName);
    }

    [AvaloniaFact]
    public async Task HeaderEditDone_EmptyNameClickAway_RestoresName()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        Dispatcher.UIThread.RunJobs();
        var original = model.SelectedItem!.DisplayName;
        ((ICommand)model.Rename).Execute(null);
        Dispatcher.UIThread.RunJobs();
        var box = view.GetVisualDescendants().OfType<TextBox>()
            .Single(x => x.Classes.Contains("tab-header-edit"));

        box.Text = "   ";
        view.MouseDown(new(20, view.Bounds.Height - 20), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.False(model.SelectedItem.IsEditingHeader);
        Assert.Equal(original, model.SelectedItem.DisplayName);
    }

    [AvaloniaFact]
    public async Task HeaderEditDone_ViewerSurfaceClicked_CommitsBoundName()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        await model.Run.Execute();
        Dispatcher.UIThread.RunJobs();
        ((ICommand)model.Rename).Execute(null);
        Dispatcher.UIThread.RunJobs();

        view.KeyTextInput("Viewer name");
        var host = view.GetVisualDescendants().OfType<SynthPlayerHost>().Single();
        var point = host.TranslatePoint(new(20, 20), view)!.Value;
        view.MouseDown(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        var viewer = Assert.IsType<ViewerViewModel>(model.SelectedItem);
        Assert.False(viewer.IsEditingHeader);
        Assert.Equal("Viewer name", viewer.DisplayName);
    }

    [AvaloniaFact]
    public void HasCapture_ClickOutside_ReleasesCapture()
    {
        var box = new TextBox { Width = 80, Height = 24 };
        var outside = new Border
        {
            Width = 100,
            Height = 80,
            Background = Brushes.Gray
        };
        var view = new Window
        {
            Width = 400,
            Height = 200,
            Content = new StackPanel { Children = { box, outside } }
        };
        using var window = TestSupport.Show(view);
        MouseCapture.SetHasCapture(box, true);
        box.Focus();
        Dispatcher.UIThread.RunJobs();

        var point = outside.TranslatePoint(new(10, 10), view)!.Value;
        view.MouseDown(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.False(MouseCapture.GetHasCapture(box));
    }

    [AvaloniaFact]
    public async Task WhenVisible_HeaderEditorShown_FocusesAndSelectsText()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        Dispatcher.UIThread.RunJobs();

        ((ICommand)model.Rename).Execute(null);
        Dispatcher.UIThread.RunJobs();

        var box = view.GetVisualDescendants().OfType<TextBox>()
            .Single(x => x.Classes.Contains("tab-header-edit"));
        Assert.True(box.IsFocused);
        Assert.Equal(box.Text!.Length, Math.Abs(box.SelectionEnd - box.SelectionStart));
    }

    [AvaloniaFact]
    public async Task New_Executed_FocusesScriptEditor()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.Load.Execute();
        Dispatcher.UIThread.RunJobs();

        await model.New.Execute();
        Dispatcher.UIThread.RunJobs();

        Assert.True(VisibleEditor(view).TextArea.IsFocused);
    }

    [AvaloniaFact]
    public async Task NewAviSynth_Executed_FocusesScriptEditor()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.Load.Execute();
        Dispatcher.UIThread.RunJobs();

        await model.NewAviSynth.Execute();
        Dispatcher.UIThread.RunJobs();

        Assert.True(VisibleEditor(view).TextArea.IsFocused);
        Assert.Equal(ScriptKind.AviSynth, Assert.IsType<EditorViewModel>(model.SelectedItem).Kind);
    }

    [AvaloniaFact]
    public async Task ReadScriptFileAsync_OpensDocument_FocusesScriptEditor()
    {
        using var file = new TestSupport.TemporaryScript("clip = core.std.BlankClip()");
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.Load.Execute();
        Dispatcher.UIThread.RunJobs();

        await model.ReadScriptFileAsync(file.Path);
        Dispatcher.UIThread.RunJobs();

        Assert.True(VisibleEditor(view).TextArea.IsFocused);
        Assert.Equal(file.Path, Assert.IsType<EditorViewModel>(model.SelectedItem).FileName);
    }

    [AvaloniaFact]
    public void LostFocus_CommandReplaced_DetachesOldHandler()
    {
        var control = new TextBox();
        var firstCalls = 0;
        var secondCalls = 0;
        using var first = ReactiveCommand.Create(() => firstCalls++);
        using var second = ReactiveCommand.Create(() => secondCalls++);
        EventCommands.SetLostFocus(control, first);

        EventCommands.SetLostFocus(control, second);
        control.RaiseEvent(new FocusChangedEventArgs(InputElement.LostFocusEvent));
        EventCommands.SetLostFocus(control, null);
        control.RaiseEvent(new FocusChangedEventArgs(InputElement.LostFocusEvent));

        Assert.Equal(0, firstCalls);
        Assert.Equal(1, secondCalls);
    }

    [AvaloniaFact]
    public async Task SeekKeys_HeaderEditorFocused_LeavesPositionUnchanged()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        await model.Run.Execute();
        Dispatcher.UIThread.RunJobs();
        var viewer = Assert.IsType<ViewerViewModel>(model.SelectedItem);
        viewer.Duration = TimeSpan.FromSeconds(239);
        viewer.Position = TimeSpan.FromSeconds(20);
        ((ICommand)model.Rename).Execute(null);
        Dispatcher.UIThread.RunJobs();
        view.KeyTextInput("Renamed");

        TestSupport.Press(view, Key.Left);

        Assert.Equal(TimeSpan.FromSeconds(20), viewer.Position);
        Assert.True(viewer.IsEditingHeader);
        Assert.Equal("Renamed", viewer.DisplayName);
    }

    [AvaloniaFact]
    public async Task PlayPauseKeys_HeaderEditorFocused_LeavesIsPlayingUnchanged()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        await model.Run.Execute();
        Dispatcher.UIThread.RunJobs();
        var viewer = Assert.IsType<ViewerViewModel>(model.SelectedItem);
        ((ICommand)model.Rename).Execute(null);
        Dispatcher.UIThread.RunJobs();
        view.KeyTextInput("Name");

        TestSupport.Press(view, Key.Space);

        Assert.False(viewer.IsPlaying);
        Assert.True(viewer.IsEditingHeader);
        Assert.Equal("Name", viewer.DisplayName);
    }

    [AvaloniaFact]
    public void DropFiles_TextDropped_IgnoresDrop()
    {
        var paths = new List<string>();
        using var command = ReactiveCommand.Create<IEnumerable<string>>(files => paths.AddRange(files));
        var view = new Window { Width = 200, Height = 200, Background = Brushes.White };
        DragDrop.SetAllowDrop(view, true);
        FileDropBehavior.SetCommand(view, command);
        using var window = TestSupport.Show(view);
        using var data = new DataTransfer();
        data.Add(DataTransferItem.CreateText("not a file"));

        view.DragDrop(new(20, 80), RawDragEventType.DragEnter, data, DragDropEffects.Copy, RawInputModifiers.None);
        view.DragDrop(new(20, 80), RawDragEventType.DragOver, data, DragDropEffects.Copy, RawInputModifiers.None);
        view.DragDrop(new(20, 80), RawDragEventType.Drop, data, DragDropEffects.Copy, RawInputModifiers.None);

        Assert.Empty(paths);
    }

    [AvaloniaFact]
    public async Task DropFiles_FileDroppedOnEditor_OpensScriptWithoutInsertingPath()
    {
        using var file = new TestSupport.TemporaryScript("clip = core.std.BlankClip()");
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        Dispatcher.UIThread.RunJobs();
        var editor = view.GetVisualDescendants().OfType<BindableTextEditor>().Single();
        var original = editor.Text;
        var storage = await view.StorageProvider.TryGetFileFromPathAsync(new(file.Path));
        Assert.NotNull(storage);
        using var data = new DataTransfer();
        data.Add(DataTransferItem.CreateFile(storage));
        data.Add(DataTransferItem.CreateText(file.Path));
        var point = editor.TranslatePoint(new(20, 20), view)!.Value;

        view.DragDrop(point, RawDragEventType.DragEnter, data, DragDropEffects.Copy, RawInputModifiers.None);
        view.DragDrop(point, RawDragEventType.DragOver, data, DragDropEffects.Copy, RawInputModifiers.None);
        view.DragDrop(point, RawDragEventType.Drop, data, DragDropEffects.Copy, RawInputModifiers.None);
        for (var i = 0; i < 50 && (model.SelectedItem as EditorViewModel)?.FileName != file.Path; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }

        var opened = Assert.IsType<EditorViewModel>(model.SelectedItem);
        Assert.Equal(file.Path, opened.FileName);
        Assert.Equal("clip = core.std.BlankClip()", opened.Script);
        Assert.Equal(original, editor.Text);
        Assert.DoesNotContain(file.Path, editor.Text);
        Assert.True(VisibleEditor(view).TextArea.IsFocused);
    }

    [AvaloniaFact]
    public async Task ZoomKeys_EditorFocused_DoNotStealEqualsOrMinus()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model, Width = 640, Height = 320 };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        Dispatcher.UIThread.RunJobs();
        var editor = VisibleEditor(view);
        editor.Focus();
        editor.Text = "clip";
        editor.CaretOffset = 4;
        Dispatcher.UIThread.RunJobs();
        var zoom = model.Zoom;

        TestSupport.Press(view, Key.OemPlus);
        view.KeyTextInput("=");
        TestSupport.Press(view, Key.OemMinus);
        view.KeyTextInput("-");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("clip=-", editor.Text);
        Assert.Equal(zoom, model.Zoom);
    }

    [AvaloniaFact]
    public async Task CopyFrameKeys_EditorFocused_LeavesClipboardUnchanged()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        await model.Run.Execute();
        Dispatcher.UIThread.RunJobs();
        view.GetVisualDescendants().OfType<SynthPlayerHost>().Single().PresentTestFrame(CreateFrame());
        Dispatcher.UIThread.RunJobs();
        model.SelectedItem = model.ScriptList.OfType<EditorViewModel>().Last();
        Dispatcher.UIThread.RunJobs();
        var editor = view.GetVisualDescendants().OfType<BindableTextEditor>()
            .First(x => x.IsEffectivelyVisible);
        editor.Focus();
        Dispatcher.UIThread.RunJobs();
        var clipboard = view.Clipboard!;
        await clipboard.SetTextAsync("keep");

        TestSupport.Press(view, Key.C, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(await clipboard.TryGetBitmapAsync());
    }

    private static WriteableBitmap CreateFrame() =>
        new(new(8, 8), new(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

    private static BindableTextEditor VisibleEditor(Visual root) =>
        root.GetVisualDescendants().OfType<BindableTextEditor>().First(x => x.IsEffectivelyVisible);
}
