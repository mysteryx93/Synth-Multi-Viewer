using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HanumanInstitute.SynthMultiViewer.ViewModels;
using HanumanInstitute.SynthMultiViewer.Views;
using ReactiveUI.Builder;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class StartScreenTests
{
    static StartScreenTests() =>
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();

    private static HeadlessUnitTestSession UiSession =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestApplication).Assembly);

    [Fact]
    public Task EmptyWorkspace_ShowsStartActionsAndKeepsToolbar() => UiSession.Dispatch(() =>
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model, Width = 640, Height = 400 };
        using var window = TestSupport.Show(view);
        Dispatcher.UIThread.RunJobs();

        var start = view.FindControl<ScrollViewer>("StartCanvas")!;
        Assert.True(start.IsVisible);
        Assert.Equal(model.New, view.FindControl<Button>("StartNewVapourSynth")!.Command);
        Assert.Equal("New VapourSynth Script", view.FindControl<Button>("StartNewVapourSynth")!.Content);
        Assert.Equal(model.NewAviSynth, view.FindControl<Button>("StartNewAviSynth")!.Command);
        Assert.Equal("New AviSynth Script", view.FindControl<Button>("StartNewAviSynth")!.Content);
        Assert.Equal(model.Open, view.FindControl<Button>("StartOpen")!.Command);
        Assert.Equal("Open Existing", view.FindControl<Button>("StartOpen")!.Content);
        var vs = view.FindControl<Button>("StartNewVapourSynth")!;
        var avs = view.FindControl<Button>("StartNewAviSynth")!;
        var open = view.FindControl<Button>("StartOpen")!;
        Assert.True(vs.Bounds.Width > 0);
        Assert.Equal(vs.Bounds.Width, avs.Bounds.Width);
        Assert.Equal(vs.Bounds.Width, open.Bounds.Width);
        var texts = start.GetVisualDescendants().OfType<TextBlock>().Select(x => x.Text).ToList();
        Assert.Contains("VapourSynth/AviSynth Editor and Viewer", texts);
        Assert.Contains("Drop a script here", texts);
        Assert.DoesNotContain(texts, x => x?.Contains("not detected") == true);
        var canvas = start.GetVisualDescendants().OfType<StackPanel>().First(x => x.MaxWidth == 560);
        var origin = canvas.TranslatePoint(default, start)!.Value;
        Assert.InRange(origin.Y + canvas.Bounds.Height / 2, start.Bounds.Height / 2 - 8, start.Bounds.Height / 2 + 8);

        var toolbar = view.GetVisualDescendants().OfType<StackPanel>().First(x => x.Classes.Contains("toolbar"));
        Assert.Contains(toolbar.Children.OfType<Button>(), x => x.Command == model.New);
        Assert.Contains(toolbar.Children.OfType<Button>(), x => x.Command == model.NewAviSynth);
        Assert.Contains(toolbar.Children.OfType<Button>(), x => x.Command == model.Open);
        Assert.Contains(toolbar.Children.OfType<Button>(), x => x.Command == model.Settings);
        Assert.Contains(toolbar.Children.OfType<Button>(), x => x.Command == model.Help);
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task New_HidesStart_CloseLastTabShowsStart() => UiSession.Dispatch(async () =>
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model, Width = 640, Height = 400 };
        using var window = TestSupport.Show(view);
        var start = view.FindControl<ScrollViewer>("StartCanvas")!;

        await model.New.Execute();
        Dispatcher.UIThread.RunJobs();
        Assert.False(start.IsVisible);

        await model.SelectedItem!.Close.Execute();
        Dispatcher.UIThread.RunJobs();
        Assert.True(start.IsVisible);
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Recents_ShownOnlyOnStartCanvas() => UiSession.Dispatch(async () =>
    {
        using var file = new TestSupport.TemporaryScript("clip");
        var settings = new TestSupport.MemorySettingsProvider();
        var model = TestSupport.CreateMain(settings: settings);
        var view = new MainView { DataContext = model, Width = 640, Height = 400 };
        using var window = TestSupport.Show(view);

        Assert.True(await model.ReadScriptFileAsync(file.Path));
        Dispatcher.UIThread.RunJobs();
        var recents = view.FindControl<ItemsControl>("StartRecents")!;
        var panel = view.FindControl<StackPanel>("StartRecentsPanel")!;
        Assert.False(panel.IsVisible);

        await model.SelectedItem!.Close.Execute();
        Dispatcher.UIThread.RunJobs();

        Assert.True(panel.IsVisible);
        Assert.Contains("Recent:", view.FindControl<ScrollViewer>("StartCanvas")!.GetVisualDescendants()
            .OfType<TextBlock>().Where(x => x.IsVisible).Select(x => x.Text));
        var recent = recents.GetVisualDescendants().OfType<Button>().Single();
        Assert.Equal(Path.GetFileName(file.Path), Assert.IsType<TextBlock>(recent.Content).Text);
        Assert.Equal(file.Path, recent.CommandParameter);
        var actions = view.FindControl<StackPanel>("StartActions")!;
        Assert.Equal(actions.Bounds.Width, panel.Bounds.Width);
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task LongRecentName_DoesNotWidenActions() => UiSession.Dispatch(async () =>
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(dir.FullName, new string('a', 80) + ".vpy");
            await File.WriteAllTextAsync(path, "clip");
            var settings = new TestSupport.MemorySettingsProvider();
            var model = TestSupport.CreateMain(settings: settings);
            var view = new MainView { DataContext = model, Width = 640, Height = 400 };
            using var window = TestSupport.Show(view);

            Assert.True(await model.ReadScriptFileAsync(path));
            await model.SelectedItem!.Close.Execute();
            Dispatcher.UIThread.RunJobs();

            var actions = view.FindControl<StackPanel>("StartActions")!;
            var panel = view.FindControl<StackPanel>("StartRecentsPanel")!;
            var recent = view.FindControl<ItemsControl>("StartRecents")!
                .GetVisualDescendants().OfType<Button>().Single();
            var label = Assert.Single(recent.GetVisualDescendants().OfType<TextBlock>());

            Assert.Equal(actions.Bounds.Width, panel.Bounds.Width);
            Assert.True(recent.Bounds.Width <= actions.Bounds.Width + 0.5);
            Assert.Equal(TextTrimming.CharacterEllipsis, label.TextTrimming);
            Assert.Equal(path, recent.CommandParameter);
            return true;
        }
        finally
        {
            dir.Delete(true);
        }
    }, TestContext.Current.CancellationToken);

}
