using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HanumanInstitute.MediaSynthUI;
using HanumanInstitute.SynthMultiViewer.ViewModels;
using HanumanInstitute.SynthMultiViewer.Views;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class MainViewModelTests
{
    [AvaloniaFact]
    public async Task Load_FileUriArgument_OpensScript()
    {
        using var file = new TestSupport.TemporaryScript("opened from uri");
        var uri = new Uri(file.Path).AbsoluteUri;
        var model = TestSupport.CreateMain(new TestSupport.TestEnvironment(["viewer", uri]));

        await model.Load.Execute();

        var editor = Assert.IsType<EditorViewModel>(Assert.Single(model.ScriptList));
        Assert.Equal(file.Path, editor.FileName);
        Assert.Equal("opened from uri", editor.Script);
    }

    [AvaloniaFact]
    public async Task Load_ExecutedTwice_OpensArgumentsOnce()
    {
        using var file = new TestSupport.TemporaryScript("script from file");
        var model = TestSupport.CreateMain(new TestSupport.TestEnvironment(["viewer", file.Path]));

        await model.Load.Execute();
        await model.Load.Execute();

        var editor = Assert.IsType<EditorViewModel>(Assert.Single(model.ScriptList));
        Assert.Equal(file.Path, editor.FileName);
        Assert.Equal("script from file", editor.Script);
        Assert.True(editor.IsActive);
    }

    [AvaloniaTheory]
    [InlineData(-1)]
    [InlineData("-1")]
    [InlineData(10)]
    [InlineData("invalid")]
    public async Task SelectEditor_InvalidIndex_LeavesSelectionUnchanged(object index)
    {
        var model = TestSupport.CreateMain();
        await model.New.Execute();
        var editor = model.SelectedItem;

        await model.SelectEditor.Execute(index);

        Assert.Same(editor, model.SelectedItem);
    }

    [AvaloniaTheory]
    [InlineData(-1)]
    [InlineData("-1")]
    [InlineData(10)]
    [InlineData("invalid")]
    public async Task SelectViewer_InvalidIndex_LeavesSelectionUnchanged(object index)
    {
        var model = TestSupport.CreateMain();
        await model.New.Execute();
        await model.Run.Execute();
        var viewer = model.SelectedItem;

        await model.SelectViewer.Execute(index);

        Assert.Same(viewer, model.SelectedItem);
    }

    [AvaloniaFact]
    public async Task NextTab_FromFirst_SelectsFollowingTab()
    {
        var model = TestSupport.CreateMain();
        await model.New.Execute();
        var first = model.SelectedItem;
        await model.New.Execute();
        var second = model.SelectedItem;
        model.SelectedItem = first;

        await model.NextTab.Execute();

        Assert.Same(second, model.SelectedItem);
    }

    [AvaloniaFact]
    public async Task NextTab_FromLast_WrapsToFirst()
    {
        var model = TestSupport.CreateMain();
        await model.New.Execute();
        var first = model.SelectedItem;
        await model.New.Execute();

        await model.NextTab.Execute();

        Assert.Same(first, model.SelectedItem);
    }

    [AvaloniaFact]
    public async Task PreviousTab_FromFirst_WrapsToLast()
    {
        var model = TestSupport.CreateMain();
        await model.New.Execute();
        var first = model.SelectedItem;
        await model.New.Execute();
        var last = model.SelectedItem;
        model.SelectedItem = first;

        await model.PreviousTab.Execute();

        Assert.Same(last, model.SelectedItem);
    }

    [AvaloniaFact]
    public async Task SelectViewer_FromEditor_SelectsViewer()
    {
        var model = TestSupport.CreateMain();
        await model.New.Execute();
        var editor = model.SelectedItem;
        await model.Run.Execute();
        var viewer = model.SelectedItem;
        model.SelectedItem = editor;

        await model.SelectViewer.Execute(0);

        Assert.Same(viewer, model.SelectedItem);
    }

    [AvaloniaTheory]
    [InlineData(1, 11)]
    [InlineData(-1, 9)]
    [InlineData(10, 20)]
    [InlineData(-10, 0)]
    [InlineData(100, 50)]
    public async Task Seek_ViewerSelected_MovesPositionByFrames(int frames, int expected)
    {
        var model = TestSupport.CreateMain();
        await model.New.Execute();
        await model.Run.Execute();
        var viewer = Assert.IsType<ViewerViewModel>(model.SelectedItem);
        viewer.Duration = TimeSpan.FromSeconds(50);
        viewer.Position = TimeSpan.FromSeconds(10);

        await model.Seek.Execute(frames);

        Assert.Equal(TimeSpan.FromSeconds(expected), viewer.Position);
    }

    [AvaloniaTheory]
    [InlineData("invalid")]
    [InlineData(null)]
    public async Task Seek_InvalidParameter_LeavesPositionUnchanged(object? frames)
    {
        var model = TestSupport.CreateMain();
        await model.New.Execute();
        await model.Run.Execute();
        var viewer = Assert.IsType<ViewerViewModel>(model.SelectedItem);
        viewer.Duration = TimeSpan.FromSeconds(50);
        viewer.Position = TimeSpan.FromSeconds(10);

        await model.Seek.Execute(frames);

        Assert.Equal(TimeSpan.FromSeconds(10), viewer.Position);
    }

    [AvaloniaFact]
    public async Task Seek_EditorSelected_CanExecuteIsFalse()
    {
        var model = TestSupport.CreateMain();
        await model.New.Execute();

        Assert.False(((ICommand)model.Seek).CanExecute(1));
    }

    [AvaloniaFact]
    public async Task PlayPause_ViewerSelected_TogglesIsPlaying()
    {
        var model = TestSupport.CreateMain();
        await model.New.Execute();
        await model.Run.Execute();
        var viewer = Assert.IsType<ViewerViewModel>(model.SelectedItem);

        await model.PlayPause.Execute();
        var playing = viewer.IsPlaying;
        await model.PlayPause.Execute();

        Assert.True(playing);
        Assert.False(viewer.IsPlaying);
    }

    [AvaloniaFact]
    public async Task PlayPause_EditorSelected_CanExecuteIsFalse()
    {
        var model = TestSupport.CreateMain();
        await model.New.Execute();

        Assert.False(((ICommand)model.PlayPause).CanExecute(null));
    }

    [AvaloniaFact]
    public async Task Close_ViewerSelected_ReleasesScriptAndSelectsRemainingTab()
    {
        var model = TestSupport.CreateMain();
        await model.New.Execute();
        var editor = model.SelectedItem;
        await model.Run.Execute();
        var viewer = (ViewerViewModel)model.SelectedItem!;

        await viewer.Close.Execute();

        Assert.Null(viewer.Script);
        Assert.False(viewer.IsActive);
        Assert.Same(editor, model.SelectedItem);
        Assert.True(editor!.IsActive);
    }

    [AvaloniaFact]
    public async Task Close_LastTabClosed_ClearsSelection()
    {
        var model = TestSupport.CreateMain();
        await model.New.Execute();

        await model.SelectedItem!.Close.Execute();

        Assert.Empty(model.ScriptList);
        Assert.Null(model.SelectedItem);
    }

    [AvaloniaFact]
    public async Task Close_MiddleTabClosed_SelectsFollowingTab()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        await model.New.Execute();
        var second = model.SelectedItem;
        await model.New.Execute();
        var third = model.SelectedItem;
        model.SelectedItem = second;
        Dispatcher.UIThread.RunJobs();

        await second!.Close.Execute();
        Dispatcher.UIThread.RunJobs();

        var strip = view.GetVisualDescendants().OfType<TabStrip>().First();
        Assert.Same(third, model.SelectedItem);
        Assert.Same(third, strip.SelectedItem);
        Assert.True(third!.IsActive);
    }

    [AvaloniaFact]
    public async Task Close_LastOfSeveralClosed_SelectsPreviousTab()
    {
        var model = TestSupport.CreateMain();
        await model.New.Execute();
        await model.New.Execute();
        var second = model.SelectedItem;
        await model.New.Execute();

        await model.SelectedItem!.Close.Execute();

        Assert.Same(second, model.SelectedItem);
        Assert.True(second!.IsActive);
    }

    [AvaloniaFact]
    public async Task UpdateAll_ViewerCountChanges_UpdatesAvailability()
    {
        var model = TestSupport.CreateMain();
        var command = (ICommand)model.UpdateAll;
        var availability = new List<bool> { command.CanExecute(null) };
        await model.New.Execute();
        var editor = model.SelectedItem;

        await model.Run.Execute();
        availability.Add(command.CanExecute(null));
        model.SelectedItem = editor;
        await model.Run.Execute();
        availability.Add(command.CanExecute(null));
        await model.SelectedItem!.Close.Execute();
        availability.Add(command.CanExecute(null));

        Assert.Equal([false, false, true, false], availability);
    }

    [AvaloniaTheory(Timeout = 10000)]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Close_InputDialogConfirmedOrCancelled_ReturnsDialogResult(bool accept)
    {
        var ownerModel = TestSupport.CreateMain();
        var owner = new Avalonia.Controls.Window { DataContext = ownerModel };
        using var window = TestSupport.Show(owner);
        var dialogs = new HanumanInstitute.MvvmDialogs.Avalonia.DialogService(new TestSupport.OwnerDialogManager(owner));
        var input = new InputViewModel { Value = "12", Validate = text => int.TryParse(text, out _) };
        input.Reset();
        var result = dialogs.ShowDialogAsync(ownerModel, input);

        ((ICommand)(accept ? input.Ok : input.Close)).Execute(null);
        var actual = await result.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(accept ? true : (bool?)null, actual);
    }

    [AvaloniaFact(Timeout = 10000)]
    public async Task Close_HelpDialogClosed_CompletesDialog()
    {
        var ownerModel = TestSupport.CreateMain();
        var owner = new Avalonia.Controls.Window { DataContext = ownerModel };
        using var window = TestSupport.Show(owner);
        var dialogs = new HanumanInstitute.MvvmDialogs.Avalonia.DialogService(new TestSupport.OwnerDialogManager(owner));
        var help = new HelpViewModel(new TestSupport.TestEnvironment());
        var result = dialogs.ShowDialogAsync(ownerModel, help);

        ((ICommand)help.Close).Execute(null);
        var actual = await result.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(actual);
    }

    [AvaloniaFact]
    public void HelpView_Shown_UsesStandardWindowChrome()
    {
        var help = new HelpView { DataContext = new HelpViewModel(new TestSupport.TestEnvironment()) };
        var settings = new SettingsView
        {
            DataContext = new SettingsViewModel(
                new TestSupport.MemorySettingsProvider(),
                new TestSupport.MemoryAppTheme(),
                new TestSupport.MemoryFrameworkDetection())
        };

        using var helpWindow = TestSupport.Show(help);
        using var settingsWindow = TestSupport.Show(settings);

        Assert.Equal(settings.WindowDecorations, help.WindowDecorations);
        Assert.Equal(WindowDecorations.Full, help.WindowDecorations);
        Assert.False(help.CanResize);
        Assert.False(help.ShowInTaskbar);
    }

    [AvaloniaFact]
    public void HelpView_AuthorName_LinksToHanumanInstitute()
    {
        var help = new HelpView { DataContext = new HelpViewModel(new TestSupport.TestEnvironment()) };

        using var shown = TestSupport.Show(help);
        var links = help.GetVisualDescendants().OfType<HyperlinkButton>().ToList();
        var link = Assert.Single(links, x => Equals(x.Content, "Etienne Charland"));

        Assert.Equal(new Uri("https://www.hanumaninstitute.com"), link.NavigateUri);
    }

    [AvaloniaFact]
    public void HelpView_GitHub_LinksToRepository()
    {
        var help = new HelpView { DataContext = new HelpViewModel(new TestSupport.TestEnvironment()) };

        using var shown = TestSupport.Show(help);
        var link = Assert.Single(help.GetVisualDescendants().OfType<HyperlinkButton>(),
            x => Equals(x.Content, "GitHub"));

        Assert.Equal(new Uri("https://github.com/mysteryx93/Synth-Multi-Viewer"), link.NavigateUri);
        Assert.Equal(Dock.Right, DockPanel.GetDock(link));
    }

    [AvaloniaFact]
    public void Threads_MultiThreadingOff_ReturnsOne()
    {
        var settings = new TestSupport.MemorySettingsProvider { Value = { VapourSynthThreads = 8 } };
        var model = TestSupport.CreateMain(settings: settings);

        Assert.Equal(1, model.Threads);
    }

    [AvaloniaFact]
    public void Threads_MultiThreadingOn_UsesVapourSynthThreads()
    {
        var settings = new TestSupport.MemorySettingsProvider { Value = { VapourSynthThreads = 8 } };
        var model = TestSupport.CreateMain(settings: settings);
        model.IsMultiThreaded = true;

        Assert.Equal(8, model.Threads);
    }

    [AvaloniaFact]
    public void Threads_SettingsSavedWhileMultiThreaded_RaisesThreads()
    {
        var settings = new TestSupport.MemorySettingsProvider { Value = { VapourSynthThreads = 4 } };
        var model = TestSupport.CreateMain(settings: settings);
        model.IsMultiThreaded = true;
        var raised = false;
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Threads))
            {
                raised = true;
            }
        };

        settings.Value.VapourSynthThreads = 12;
        settings.Save();

        Assert.True(raised);
        Assert.Equal(12, model.Threads);
    }

    [Fact]
    public void Zoom_SetToZero_EnablesScaleToFit()
    {
        var model = TestSupport.CreateMain();

        model.Zoom = 0;

        Assert.True(model.ZoomScaleToFit);
        Assert.Equal(0, model.Zoom);
    }

    [AvaloniaFact]
    public void ZoomCombo_Shown_IsEditableAndListsScaleToFit()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        TestSupport.ShowViewerToolbar(model);
        var combo = view.FindControl<ComboBox>("ZoomCombo")!;

        Assert.True(combo.IsEditable);
        Assert.Contains("Scale to Fit", model.ZoomList);
        Assert.Equal(model.ZoomList, combo.ItemsSource);
        Assert.Equal("100%", combo.Text);
        var box = combo.GetVisualDescendants().OfType<TextBox>()
            .Single(x => x.Name == "PART_EditableTextBox");
        var boxPos = box.TranslatePoint(new Point(0, 0), combo)!.Value;
        Assert.InRange(combo.Bounds.Height, 26, 32);
        Assert.InRange(boxPos.Y, -1, 4);
        Assert.True(box.Bounds.Height <= combo.Bounds.Height + 1);
        Assert.True(box.Bounds.Width < combo.Bounds.Width);
    }

    [AvaloniaFact]
    public void ZoomCombo_SelectScaleToFit_EnablesScaleToFit()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        TestSupport.ShowViewerToolbar(model);
        var combo = view.FindControl<ComboBox>("ZoomCombo")!;

        combo.SelectedItem = "Scale to Fit";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, model.Zoom);
        Assert.True(model.ZoomScaleToFit);
        Assert.Equal("Scale to Fit", combo.Text);
    }

    [AvaloniaFact]
    public void ZoomCombo_TypeIncompleteText_LeavesZoomUnchanged()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        TestSupport.ShowViewerToolbar(model);
        var combo = view.FindControl<ComboBox>("ZoomCombo")!;
        var box = combo.GetVisualDescendants().OfType<TextBox>()
            .Single(x => x.Name == "PART_EditableTextBox");

        box.Focus();
        box.Text = "abc";
        view.GetVisualDescendants().OfType<Button>().Single(b => Equals(ToolTip.GetTip(b), "Help (F1)")).Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, model.Zoom);
        Assert.False(model.ZoomScaleToFit);
        Assert.Equal("100%", combo.Text);
    }

    [AvaloniaFact]
    public void ZoomCombo_TypePercentageAndCommit_UpdatesZoom()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        TestSupport.ShowViewerToolbar(model);
        var combo = view.FindControl<ComboBox>("ZoomCombo")!;
        var box = combo.GetVisualDescendants().OfType<TextBox>()
            .Single(x => x.Name == "PART_EditableTextBox");

        box.Focus();
        box.Text = "150%";
        view.GetVisualDescendants().OfType<Button>().Single(b => Equals(ToolTip.GetTip(b), "Help (F1)")).Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1.5, model.Zoom);
        Assert.False(model.ZoomScaleToFit);
        Assert.Equal("150%", combo.Text);
    }

    [AvaloniaFact]
    public async Task Toolbar_EditorSelected_ShowsEditorCommands()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        Dispatcher.UIThread.RunJobs();

        Assert.True(model.IsEditorSelected);
        Assert.False(model.IsViewerSelected);
        Assert.False(model.IsVapourSynthViewerSelected);
        AssertToolbar(view, editor: true, viewer: false, vsViewer: false);
    }

    [AvaloniaFact]
    public async Task Toolbar_VapourSynthViewerSelected_ShowsViewerCommands()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await TestSupport.OpenViewerAsync(model);

        Assert.False(model.IsEditorSelected);
        Assert.True(model.IsViewerSelected);
        Assert.True(model.IsVapourSynthViewerSelected);
        AssertToolbar(view, editor: false, viewer: true, vsViewer: true);
    }

    [AvaloniaFact]
    public async Task Toolbar_AviSynthViewerSelected_HidesMultiThreading()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.NewAviSynth.Execute();
        await model.Run.Execute();
        Dispatcher.UIThread.RunJobs();

        Assert.False(model.IsEditorSelected);
        Assert.True(model.IsViewerSelected);
        Assert.False(model.IsVapourSynthViewerSelected);
        AssertToolbar(view, editor: false, viewer: true, vsViewer: false);
    }

    private static void AssertToolbar(MainView view, bool editor, bool viewer, bool vsViewer)
    {
        Assert.True(TipVisible(view, "New VapourSynth script (Ctrl+N)"));
        Assert.True(TipVisible(view, "New AviSynth script (Ctrl+M)"));
        Assert.True(TipVisible(view, "Open file... (Ctrl+O)"));
        Assert.Equal(editor, TipVisible(view, "Save (Ctrl+S)"));
        Assert.Equal(editor, TipVisible(view, "Save as... (Ctrl+Shift+S)"));
        Assert.Equal(editor, TipVisible(view, "Run script (F5)"));
        Assert.Equal(viewer, TipVisible(view, "Go to frame... (Ctrl+G)"));
        Assert.Equal(viewer, TipVisible(view, "Copy frame to clipboard (Ctrl+C)"));
        Assert.Equal(vsViewer, TipVisible(view, "Enable multi-threading (F8)"));
        Assert.Equal(viewer, TipVisible(view, "Square Pixels (F9)"));
        Assert.Equal(viewer, TipVisible(view, "Load frame in all tabs (Ctrl+F6)"));
        Assert.Equal(viewer, view.FindControl<ComboBox>("ZoomCombo")!.IsVisible);
        Assert.True(TipVisible(view, "Settings (F3)"));
        Assert.True(TipVisible(view, "Help (F1)"));
        var toolbar = view.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Classes.Contains("toolbar"));
        foreach (var image in toolbar.GetVisualDescendants().OfType<Image>())
        {
            Assert.Equal(24, image.Width);
            Assert.Equal(24, image.Height);
            Assert.Equal(Stretch.None, image.Stretch);
            if (image.IsEffectivelyVisible)
            {
                Assert.Equal(24, image.Bounds.Width);
                Assert.Equal(24, image.Bounds.Height);
            }
        }
    }

    private static bool TipVisible(Visual root, string tip) =>
        root.GetVisualDescendants().OfType<Control>().Single(c => Equals(ToolTip.GetTip(c), tip)).IsVisible;

    [AvaloniaFact]
    public async Task Run_AviSynthEditor_CopiesKindToViewer()
    {
        var model = TestSupport.CreateMain();
        await model.NewAviSynth.Execute();

        var editor = Assert.IsType<EditorViewModel>(model.SelectedItem);
        editor.FileName = Path.Combine(Path.GetTempPath(), "clip.avs");

        await model.Run.Execute();

        var viewer = Assert.IsType<ViewerViewModel>(model.SelectedItem);
        Assert.Equal(ScriptKind.AviSynth, viewer.Kind);
        Assert.Equal(editor.Script, viewer.Script);
        Assert.Equal(editor.FileName, viewer.FileName);
    }

    [AvaloniaFact]
    public async Task ReadScriptFileAsync_AvsExtension_SetsAviSynthKind()
    {
        var path = Path.Combine(Path.GetTempPath(), $"SynthMultiViewer-{Guid.NewGuid():N}.avs");
        await File.WriteAllTextAsync(path, "BlankClip()\n");
        var model = TestSupport.CreateMain();
        try
        {
            var loaded = await model.ReadScriptFileAsync(path);

            var editor = Assert.IsType<EditorViewModel>(Assert.Single(model.ScriptList));
            Assert.True(loaded);
            Assert.Equal(path, editor.FileName);
            Assert.Equal(ScriptKind.AviSynth, editor.Kind);
            Assert.Equal("BlankClip()\n", editor.Script);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
