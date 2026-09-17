using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HanumanInstitute.SynthMultiViewer.Services;
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
        Assert.Equal(model.NewAviSynth, view.FindControl<Button>("StartNewAviSynth")!.Command);
        Assert.Equal(model.Open, view.FindControl<Button>("StartOpen")!.Command);
        Assert.Contains("Drop a script here", start.GetVisualDescendants().OfType<TextBlock>().Select(x => x.Text));
        Assert.Contains("VapourSynth and AviSynth not detected",
            start.GetVisualDescendants().OfType<TextBlock>().Select(x => x.Text));

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
        Assert.False(recents.IsVisible);

        await model.SelectedItem!.Close.Execute();
        Dispatcher.UIThread.RunJobs();

        Assert.True(recents.IsVisible);
        var recent = recents.GetVisualDescendants().OfType<Button>().Single(x => x.Classes.Contains("start-recent"));
        Assert.Equal(Path.GetFileName(file.Path), recent.Content);
        Assert.Equal(file.Path, recent.CommandParameter);
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task EngineStatus_WhenFound_IsHidden() => UiSession.Dispatch(() =>
    {
        var detection = new TestSupport.MemoryFrameworkDetection
        {
            VapourSynth = new FrameworkInstall(true),
            AviSynth = new FrameworkInstall(true)
        };
        var model = TestSupport.CreateMain(frameworks: detection);
        var view = new MainView { DataContext = model, Width = 640, Height = 400 };
        using var window = TestSupport.Show(view);
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain("not detected",
            view.FindControl<ScrollViewer>("StartCanvas")!.GetVisualDescendants()
                .OfType<TextBlock>().Select(x => x.Text));
        return true;
    }, TestContext.Current.CancellationToken);
}
