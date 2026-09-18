using System.ComponentModel;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Search;
using HanumanInstitute.MediaSynthUI;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.Avalonia;
using HanumanInstitute.MvvmDialogs.FileSystem;
using HanumanInstitute.ScriptAssist;
using HanumanInstitute.ScriptAssist.AvaloniaEdit;
using HanumanInstitute.ScriptAssist.VapourSynth;
using HanumanInstitute.SynthMultiViewer.Controls;
using HanumanInstitute.SynthMultiViewer.ViewModels;
using HanumanInstitute.SynthMultiViewer.Views;
using AvaloniaPlayer = HanumanInstitute.MediaPlayer.Avalonia.MediaPlayer;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class KeyBindingTests
{
    private static HeadlessUnitTestSession UiSession =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestApplication).Assembly);

    [Fact]
    public Task WindowBindings_FileTabsAndDialogs_Execute() => UiSession.Dispatch(async () =>
    {
        using var file = new TestSupport.TemporaryScript("BlankClip()\n");
        var dialogs = new RecordingDialogs();
        dialogs.QueueFile(file.Path);
        var model = TestSupport.CreateMain(manager: dialogs);
        var view = new MainView { DataContext = model, Width = 640, Height = 400 };
        using var shown = TestSupport.Show(view);
        view.Focus();
        Dispatcher.UIThread.RunJobs();

        TestSupport.Press(view, Key.N, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var vs = Assert.IsType<EditorViewModel>(model.SelectedItem);
        Assert.Equal(ScriptKind.VapourSynth, vs.Kind);
        vs.MarkSaved();

        TestSupport.Press(view, Key.M, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var avs = Assert.IsType<EditorViewModel>(model.SelectedItem);
        Assert.Equal(ScriptKind.AviSynth, avs.Kind);
        avs.MarkSaved();

        TestSupport.Press(view, Key.O, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(file.Path, Assert.IsType<EditorViewModel>(model.SelectedItem).FileName);

        TestSupport.Press(view, Key.D1, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.Same(vs, model.SelectedItem);

        TestSupport.Press(view, Key.Tab, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.Same(avs, model.SelectedItem);

        TestSupport.Press(view, Key.Tab, RawInputModifiers.Control | RawInputModifiers.Shift);
        Dispatcher.UIThread.RunJobs();
        Assert.Same(vs, model.SelectedItem);

        TestSupport.Press(view, Key.Right, RawInputModifiers.Alt);
        Dispatcher.UIThread.RunJobs();
        Assert.Same(vs, model.ScriptList[1]);
        TestSupport.Press(view, Key.Left, RawInputModifiers.Alt);
        Dispatcher.UIThread.RunJobs();
        Assert.Same(vs, model.ScriptList[0]);

        TestSupport.Press(view, Key.F2);
        Dispatcher.UIThread.RunJobs();
        Assert.True(vs.IsEditingHeader);
        vs.IsEditingHeader = false;
        Dispatcher.UIThread.RunJobs();

        TestSupport.Press(view, Key.T, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.IsType<TabColorViewModel>(dialogs.LastDialog);

        TestSupport.Press(view, Key.F1);
        Dispatcher.UIThread.RunJobs();
        Assert.IsType<HelpViewModel>(dialogs.LastDialog);

        TestSupport.Press(view, Key.OemComma, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.IsType<SettingsViewModel>(dialogs.LastDialog);

        TestSupport.Press(view, Key.S, RawInputModifiers.Control | RawInputModifiers.Shift);
        Dispatcher.UIThread.RunJobs();
        Assert.IsType<MvvmDialogs.FrameworkDialogs.SaveFileDialogSettings>(dialogs.LastFramework);

        var savePath = Path.Combine(Path.GetTempPath(), "keybind-save.vpy");
        dialogs.QueueSave(savePath);
        TestSupport.Press(view, Key.S, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(savePath, vs.FileName);
        File.Delete(savePath);

        TestSupport.Press(view, Key.F5);
        Dispatcher.UIThread.RunJobs();
        Assert.IsType<ViewerViewModel>(model.SelectedItem);

        var beforeClose = model.ScriptList.Count;
        TestSupport.Press(view, Key.F4, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(beforeClose - 1, model.ScriptList.Count);
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ViewerBindings_SeekPlayZoomPropertiesAndCopy_Execute() => UiSession.Dispatch(async () =>
    {
        var dialogs = new RecordingDialogs
        {
            OnShow = dialog =>
            {
                if (dialog is InputViewModel input)
                {
                    input.Value = "30";
                    ((System.Windows.Input.ICommand)input.Ok).Execute(null);
                }
            }
        };
        var model = TestSupport.CreateMain(manager: dialogs);
        var view = new MainView { DataContext = model, Width = 640, Height = 400 };
        using var shown = TestSupport.Show(view);
        await model.New.Execute();
        await model.Run.Execute();
        await model.New.Execute();
        await model.Run.Execute();
        Dispatcher.UIThread.RunJobs();
        var first = (ViewerViewModel)model.ScriptList.OfType<ViewerViewModel>().First();
        var second = (ViewerViewModel)model.SelectedItem!;
        first.Duration = TimeSpan.FromSeconds(239);
        second.Duration = TimeSpan.FromSeconds(239);
        second.Position = TimeSpan.FromSeconds(20);
        view.Focus();
        Dispatcher.UIThread.RunJobs();

        TestSupport.Press(view, Key.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(TimeSpan.FromSeconds(19), second.Position);
        TestSupport.Press(view, Key.Right);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(TimeSpan.FromSeconds(20), second.Position);
        TestSupport.Press(view, Key.Left, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(TimeSpan.FromSeconds(10), second.Position);
        TestSupport.Press(view, Key.Right, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(TimeSpan.FromSeconds(20), second.Position);

        TestSupport.Press(view, Key.Space);
        Dispatcher.UIThread.RunJobs();
        Assert.True(second.IsPlaying);
        TestSupport.Press(view, Key.Space);
        Dispatcher.UIThread.RunJobs();
        Assert.False(second.IsPlaying);

        var startZoom = model.Zoom;
        TestSupport.Press(view, Key.OemPlus);
        Dispatcher.UIThread.RunJobs();
        Assert.NotEqual(startZoom, model.Zoom);
        TestSupport.Press(view, Key.OemMinus);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(startZoom, model.Zoom);

        TestSupport.Press(view, Key.G, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(TimeSpan.FromSeconds(29), second.Position);

        TestSupport.Press(view, Key.I, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.True(model.IsPropertiesOpen);
        TestSupport.Press(view, Key.I, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.False(model.IsPropertiesOpen);

        first.Position = TimeSpan.FromSeconds(5);
        TestSupport.Press(view, Key.F6, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(second.Position, first.Position);

        var threaded = model.IsMultiThreaded;
        TestSupport.Press(view, Key.F8);
        Dispatcher.UIThread.RunJobs();
        Assert.NotEqual(threaded, model.IsMultiThreaded);

        var square = model.SquarePixels;
        TestSupport.Press(view, Key.F9);
        Dispatcher.UIThread.RunJobs();
        Assert.NotEqual(square, model.SquarePixels);

        view.GetVisualDescendants().OfType<SynthPlayerHost>().First(x => x.IsEffectivelyVisible)
            .PresentTestFrame(new WriteableBitmap(new PixelSize(8, 8), new Vector(96, 96),
                PixelFormat.Bgra8888, AlphaFormat.Opaque));
        Dispatcher.UIThread.RunJobs();
        TestSupport.Press(view, Key.C, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var copied = await view.Clipboard!.TryGetBitmapAsync();
        Assert.NotNull(copied);
        Assert.Equal(new PixelSize(8, 8), copied.PixelSize);
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task EditorBindings_AvaloniaEditAndAssist_Execute() => UiSession.Dispatch(async () =>
    {
        var factory = new CountingFactory();
        var editor = new BindableTextEditor
        {
            Text = "alpha\nbeta\n",
            LanguageService = new LanguageService(new VapourSynthLanguage(),
                new CatalogCache(() =>
                [
                    new Symbol("core.std.Crop", ["clip:vnode", "left:int:opt"], ReturnType: "clip:vnode")
                ])),
            LanguageFactory = factory
        };
        var window = new Window { Content = editor, Width = 480, Height = 240 };
        using var shown = TestSupport.Show(window);
        editor.TextArea.Focus();
        editor.CaretOffset = 0;
        Dispatcher.UIThread.RunJobs();

        TestSupport.Press(window, Key.D, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.StartsWith("beta", editor.Text, StringComparison.Ordinal);

        TestSupport.Press(window, Key.Z, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.StartsWith("alpha", editor.Text, StringComparison.Ordinal);

        TestSupport.Press(window, Key.A, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(editor.Text.Length, editor.SelectionLength);

        editor.SelectionLength = 0;
        editor.CaretOffset = 0;
        TestSupport.Press(window, Key.Tab);
        Dispatcher.UIThread.RunJobs();
        Assert.StartsWith("\t", editor.Text, StringComparison.Ordinal);

        TestSupport.Press(window, Key.Z, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.StartsWith("alpha", editor.Text, StringComparison.Ordinal);
        TestSupport.Press(window, Key.Y, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.StartsWith("\t", editor.Text, StringComparison.Ordinal);

        TestSupport.Press(window, Key.Tab, RawInputModifiers.Shift);
        Dispatcher.UIThread.RunJobs();
        Assert.StartsWith("alpha", editor.Text, StringComparison.Ordinal);

        editor.Text = "hello world\nsecond";
        editor.CaretOffset = 5;
        Dispatcher.UIThread.RunJobs();
        TestSupport.Press(window, Key.Home);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, editor.CaretOffset);
        TestSupport.Press(window, Key.End);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(11, editor.CaretOffset);
        TestSupport.Press(window, Key.End, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(editor.Text.Length, editor.CaretOffset);
        TestSupport.Press(window, Key.Home, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, editor.CaretOffset);
        TestSupport.Press(window, Key.Right, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.True(editor.CaretOffset > 0);
        TestSupport.Press(window, Key.Left, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, editor.CaretOffset);

        editor.CaretOffset = 11;
        TestSupport.Press(window, Key.Back, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.StartsWith("hello ", editor.Text, StringComparison.Ordinal);
        editor.CaretOffset = 0;
        TestSupport.Press(window, Key.Delete, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain("hello", editor.Text, StringComparison.Ordinal);

        editor.Text = "beta one\nbeta two\n";
        editor.CaretOffset = 0;
        TestSupport.Press(window, Key.F, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.False(editor.SearchPanel.IsClosed);

        TestSupport.Press(window, Key.H, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        Assert.True(editor.SearchPanel.IsReplaceMode);

        editor.SearchPanel.SearchPattern = "beta";
        TestSupport.Press(window, Key.F3);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("beta", editor.SelectedText);
        var firstMatch = editor.SelectionStart;
        TestSupport.Press(window, Key.F3);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("beta", editor.SelectedText);
        Assert.NotEqual(firstMatch, editor.SelectionStart);
        TestSupport.Press(window, Key.F3, RawInputModifiers.Shift);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(firstMatch, editor.SelectionStart);

        editor.SearchPanel.ReplacePattern = "gamma";
        editor.CaretOffset = 0;
        TestSupport.Press(window, Key.R, RawInputModifiers.Alt);
        Dispatcher.UIThread.RunJobs();
        Assert.StartsWith("gamma", editor.Text, StringComparison.Ordinal);

        TestSupport.Press(window, Key.A, RawInputModifiers.Alt);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("gamma two", editor.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("beta", editor.Text, StringComparison.Ordinal);

        TestSupport.Press(window, Key.Escape);
        Dispatcher.UIThread.RunJobs();
        Assert.True(editor.SearchPanel.IsClosed);

        editor.Text = "core.std.Cr";
        editor.CaretOffset = editor.Text.Length;
        TestSupport.Press(window, Key.Space, RawInputModifiers.Control);
        await Task.Delay(80, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(editor.Completion);

        TestSupport.Press(window, Key.Escape);
        Dispatcher.UIThread.RunJobs();
        Assert.Null(editor.DisplayedReply);

        editor.Text = "core.std.Crop(";
        editor.CaretOffset = editor.Text.Length;
        TestSupport.Press(window, Key.Space, RawInputModifiers.Control | RawInputModifiers.Shift);
        await Task.Delay(80, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(editor.DisplayedReply?.Insight);

        TestSupport.Press(window, Key.R, RawInputModifiers.Control | RawInputModifiers.Shift);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, factory.RefreshCount);
        editor.DismissCompletion();
        return true;
    }, TestContext.Current.CancellationToken);

    private sealed class CountingFactory : IScriptLanguageFactory
    {
        public int RefreshCount { get; private set; }
        public bool IsEnabled { get; set; } = true;
        public ILanguageService? Create(string language) => null;
        public void Configure(string language, string catalogKey) { }
        public void Refresh() => RefreshCount++;
    }

    private sealed class RecordingDialogs : DialogManager
    {
        public RecordingDialogs() : base(viewLocator: new ViewLocator()) { }

        public IModalDialogViewModel? LastDialog { get; private set; }
        public object? LastFramework { get; private set; }
        public Action<IModalDialogViewModel>? OnShow { get; set; }

        private readonly Queue<object?> _queued = new();

        public void QueueFile(string path) =>
            _queued.Enqueue(new IDialogStorageFile[] { new DesktopDialogStorageFile(path) });

        public void QueueSave(string path) =>
            _queued.Enqueue(new DesktopDialogStorageFile(path));

        public override void Show(INotifyPropertyChanged? ownerViewModel, INotifyPropertyChanged viewModel) =>
            LastDialog = viewModel as IModalDialogViewModel;

        public override Task ShowDialogAsync(INotifyPropertyChanged ownerViewModel, IModalDialogViewModel viewModel)
        {
            LastDialog = viewModel;
            OnShow?.Invoke(viewModel);
            return Task.CompletedTask;
        }

        public override Task<object?> ShowFrameworkDialogAsync<TSettings>(
            INotifyPropertyChanged? ownerViewModel, TSettings settings, Func<object?, string>? resultToString = null)
        {
            LastFramework = settings;
            return Task.FromResult(_queued.Count > 0 ? _queued.Dequeue() : null);
        }
    }
}
