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
using AvaloniaPlayer = HanumanInstitute.MediaPlayer.Avalonia.MediaPlayer;
using HanumanInstitute.SynthMultiViewer.Helpers;
using HanumanInstitute.SynthMultiViewer.ViewModels;
using HanumanInstitute.SynthMultiViewer.Views;
using ReactiveUI;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class BehaviorTests
{
    [AvaloniaFact]
    public void HeaderEditDone_EnterPressed_CommitsBoundName()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        ((ICommand)model.Rename).Execute(null);
        Dispatcher.UIThread.RunJobs();

        view.KeyTextInput("Renamed tab");
        TestSupport.Press(view, Key.Enter);

        Assert.Equal("Renamed tab", model.SelectedItem!.DisplayName);
        Assert.False(model.SelectedItem.IsEditingHeader);
    }

    [AvaloniaFact]
    public void HeaderEditCancel_EscapePressed_RestoresName()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        var original = model.SelectedItem!.DisplayName;
        ((ICommand)model.Rename).Execute(null);
        Dispatcher.UIThread.RunJobs();

        view.KeyTextInput("Renamed tab");
        TestSupport.Press(view, Key.Escape);

        Assert.Equal(original, model.SelectedItem.DisplayName);
        Assert.False(model.SelectedItem.IsEditingHeader);
    }

    [AvaloniaFact]
    public void HeaderEditDone_BlankName_RestoresName()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
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
    public void HeaderEditDone_EmptyNameClickAway_RestoresName()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        var original = model.SelectedItem!.DisplayName;
        ((ICommand)model.Rename).Execute(null);
        Dispatcher.UIThread.RunJobs();
        var box = view.GetVisualDescendants().OfType<TextBox>()
            .Single(x => x.Classes.Contains("tab-header-edit"));

        box.Text = "   ";
        view.MouseDown(new Point(20, view.Bounds.Height - 20), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.False(model.SelectedItem.IsEditingHeader);
        Assert.Equal(original, model.SelectedItem.DisplayName);
    }

    [AvaloniaFact]
    public void HeaderEditDone_FocusMovesAway_CommitsBoundName()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        ((ICommand)model.Rename).Execute(null);
        Dispatcher.UIThread.RunJobs();

        view.KeyTextInput("New name");
        view.GetVisualDescendants().OfType<Button>().Last().Focus();

        Assert.False(model.SelectedItem!.IsEditingHeader);
        Assert.Equal("New name", model.SelectedItem.DisplayName);
    }

    [AvaloniaFact]
    public async Task HeaderEditDone_ViewerSurfaceClicked_CommitsBoundName()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.Run.Execute();
        Dispatcher.UIThread.RunJobs();
        ((ICommand)model.Rename).Execute(null);
        Dispatcher.UIThread.RunJobs();

        view.KeyTextInput("Viewer name");
        var host = view.GetVisualDescendants().OfType<SynthPlayerHost>().Single();
        var point = host.TranslatePoint(new Point(20, 20), view)!.Value;
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

        var point = outside.TranslatePoint(new Point(10, 10), view)!.Value;
        view.MouseDown(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.False(MouseCapture.GetHasCapture(box));
    }

    [AvaloniaFact]
    public void WhenVisible_HeaderEditorShown_FocusesAndSelectsText()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);

        ((ICommand)model.Rename).Execute(null);
        Dispatcher.UIThread.RunJobs();

        var box = view.GetVisualDescendants().OfType<TextBox>()
            .Single(x => x.Classes.Contains("tab-header-edit"));

        Assert.True(box.IsFocused);
        Assert.Equal(box.Text!.Length, Math.Abs(box.SelectionEnd - box.SelectionStart));
    }

    [AvaloniaFact]
    public async Task Load_CreatesDefaultEditor_FocusesScriptEditor()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);

        await model.Load.Execute();
        Dispatcher.UIThread.RunJobs();

        Assert.True(VisibleEditor(view).TextArea.IsFocused);
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
    public async Task SeekKeys_ViewerSelected_MovesPosition()
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
        view.Focus();
        Dispatcher.UIThread.RunJobs();

        TestSupport.Press(view, Key.Left);
        TestSupport.Press(view, Key.Right);
        TestSupport.Press(view, Key.Left, RawInputModifiers.Control);
        TestSupport.Press(view, Key.Right, RawInputModifiers.Control);

        Assert.Equal(TimeSpan.FromSeconds(20), viewer.Position);
    }

    [AvaloniaFact]
    public async Task SeekKeys_HeaderEditorFocused_LeavesPositionUnchanged()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
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
    public async Task PlayPauseKeys_ViewerSelected_TogglesIsPlaying()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        await model.Run.Execute();
        Dispatcher.UIThread.RunJobs();
        var viewer = Assert.IsType<ViewerViewModel>(model.SelectedItem);
        viewer.Script = null;
        Dispatcher.UIThread.RunJobs();
        view.Focus();
        Dispatcher.UIThread.RunJobs();

        TestSupport.Press(view, Key.Space);
        var playing = viewer.IsPlaying;
        TestSupport.Press(view, Key.Space);

        Assert.True(playing);
        Assert.False(viewer.IsPlaying);
    }

    [AvaloniaFact]
    public async Task PlayPauseKeys_HeaderEditorFocused_LeavesIsPlayingUnchanged()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
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
    public async Task FullScreenKeys_ViewerSelected_OpensPlayerFullScreen()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        await model.Run.Execute();
        Dispatcher.UIThread.RunJobs();
        var player = view.GetVisualDescendants().OfType<AvaloniaPlayer>().Single();
        view.Focus();
        Dispatcher.UIThread.RunJobs();

        TestSupport.Press(view, Key.Enter, RawInputModifiers.Alt);
        Dispatcher.UIThread.RunJobs();
        var opened = player.FullScreen;
        player.FullScreen = false;
        Dispatcher.UIThread.RunJobs();

        Assert.True(opened);
        Assert.False(player.FullScreen);
    }

    [AvaloniaFact]
    public async Task FullScreen_Exit_RestoresPlayerFocusWithoutZoomCombo()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        await model.Run.Execute();
        Dispatcher.UIThread.RunJobs();
        var player = view.GetVisualDescendants().OfType<AvaloniaPlayer>().Single();
        var combo = view.FindControl<ComboBox>("ZoomCombo")!;
        view.Focus();
        Dispatcher.UIThread.RunJobs();

        TestSupport.Press(view, Key.Enter, RawInputModifiers.Alt);
        Dispatcher.UIThread.RunJobs();
        player.FullScreen = false;
        Dispatcher.UIThread.RunJobs();

        Assert.False(combo.IsKeyboardFocusWithin);
        Assert.True(player.IsFocused);
    }

    [AvaloniaFact]
    public async Task FullScreen_Toggle_EntersAndExits()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        await model.Run.Execute();
        Dispatcher.UIThread.RunJobs();
        var player = view.GetVisualDescendants().OfType<AvaloniaPlayer>().Single();
        player.ToggleFullScreenCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        var entered = player.FullScreen;
        player.FullScreen = false;
        Dispatcher.UIThread.RunJobs();

        Assert.True(entered);
        Assert.False(player.FullScreen);
    }

    [AvaloniaFact]
    public void DropFiles_TextDropped_IgnoresDrop()
    {
        var paths = new List<string>();
        using var command = ReactiveCommand.Create<IEnumerable<string>>(files => paths.AddRange(files));
        var view = new Window { Width = 200, Height = 200, Background = Avalonia.Media.Brushes.White };
        DragDrop.SetAllowDrop(view, true);
        FileDropBehavior.SetCommand(view, command);
        using var window = TestSupport.Show(view);
        using var data = new DataTransfer();
        data.Add(DataTransferItem.CreateText("not a file"));

        view.DragDrop(new Point(20, 80), RawDragEventType.DragEnter, data, DragDropEffects.Copy, RawInputModifiers.None);
        view.DragDrop(new Point(20, 80), RawDragEventType.DragOver, data, DragDropEffects.Copy, RawInputModifiers.None);
        view.DragDrop(new Point(20, 80), RawDragEventType.Drop, data, DragDropEffects.Copy, RawInputModifiers.None);

        Assert.Empty(paths);
    }

    [AvaloniaFact]
    public async Task DropFiles_FileDroppedOnEditor_OpensScriptWithoutInsertingPath()
    {
        using var file = new TestSupport.TemporaryScript("clip = core.std.BlankClip()");
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        Dispatcher.UIThread.RunJobs();
        var editor = view.GetVisualDescendants().OfType<BindableTextEditor>().Single();
        var original = editor.Text;
        var storage = await view.StorageProvider.TryGetFileFromPathAsync(new Uri(file.Path));
        Assert.NotNull(storage);
        using var data = new DataTransfer();
        data.Add(DataTransferItem.CreateFile(storage));
        data.Add(DataTransferItem.CreateText(file.Path));
        var point = editor.TranslatePoint(new Point(20, 20), view)!.Value;

        view.DragDrop(point, RawDragEventType.DragEnter, data, DragDropEffects.Copy, RawInputModifiers.None);
        view.DragDrop(point, RawDragEventType.DragOver, data, DragDropEffects.Copy, RawInputModifiers.None);
        view.DragDrop(point, RawDragEventType.Drop, data, DragDropEffects.Copy, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        var opened = Assert.IsType<EditorViewModel>(model.SelectedItem);
        Assert.Equal(file.Path, opened.FileName);
        Assert.Equal("clip = core.std.BlankClip()", opened.Script);
        Assert.Equal(original, editor.Text);
        Assert.DoesNotContain(file.Path, editor.Text);
        Assert.True(VisibleEditor(view).TextArea.IsFocused);
    }

    [AvaloniaFact]
    public async Task CopyFrameKeys_ViewerSelected_PlacesBitmapOnClipboard()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        await model.Run.Execute();
        Dispatcher.UIThread.RunJobs();
        var host = view.GetVisualDescendants().OfType<SynthPlayerHost>().Single();
        host.PresentTestFrame(CreateFrame());
        Dispatcher.UIThread.RunJobs();
        view.Focus();
        Dispatcher.UIThread.RunJobs();

        TestSupport.Press(view, Key.C, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();

        var copied = await view.Clipboard!.TryGetBitmapAsync();
        Assert.NotNull(copied);
        Assert.Equal(new PixelSize(8, 8), copied.PixelSize);
    }

    [Fact]
    public Task ZoomKeys_EditorFocused_DoNotStealEqualsOrMinus() => UiSession.Dispatch(async () =>
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
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ZoomKeys_ViewerFocused_ChangeZoom() => UiSession.Dispatch(() =>
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model, Width = 640, Height = 320 };
        using var window = TestSupport.Show(view);
        TestSupport.ShowViewerToolbar(model);
        view.Focus();
        Dispatcher.UIThread.RunJobs();
        var start = model.Zoom;

        TestSupport.Press(view, Key.OemPlus);
        Dispatcher.UIThread.RunJobs();
        Assert.NotEqual(start, model.Zoom);
        var zoomed = model.Zoom;
        TestSupport.Press(view, Key.OemMinus);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(start, model.Zoom);
        Assert.NotEqual(zoomed, model.Zoom);
        return true;
    }, TestContext.Current.CancellationToken);

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

        Assert.Equal("keep", await clipboard.TryGetTextAsync());
        Assert.Null(await clipboard.TryGetBitmapAsync());
    }

    [AvaloniaFact]
    public async Task CopyFrame_Executed_PlacesBitmapOnClipboard()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        await model.Run.Execute();
        Dispatcher.UIThread.RunJobs();
        var host = view.GetVisualDescendants().OfType<SynthPlayerHost>().Single();
        host.PresentTestFrame(CreateFrame());
        Dispatcher.UIThread.RunJobs();
        await view.Clipboard!.ClearAsync();
        var button = view.GetVisualDescendants().OfType<Button>()
            .First(x => x.Command == model.CopyFrame);
        var point = button.TranslatePoint(
            new Point(Math.Max(1, button.Bounds.Width / 2), Math.Max(1, button.Bounds.Height / 2)), view)!.Value;

        view.MouseDown(point, MouseButton.Left);
        view.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        var copied = await view.Clipboard!.TryGetBitmapAsync();
        Assert.NotNull(copied);
        Assert.Equal(new PixelSize(8, 8), copied.PixelSize);
        Assert.Same(view, button.CommandParameter);
    }

    private static WriteableBitmap CreateFrame() =>
        new(new PixelSize(8, 8), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

    private static BindableTextEditor VisibleEditor(Visual root) =>
        root.GetVisualDescendants().OfType<BindableTextEditor>().First(x => x.IsEffectivelyVisible);

    private static HeadlessUnitTestSession UiSession =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestApplication).Assembly);
}
