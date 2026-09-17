using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HanumanInstitute.MediaSynthUI;
using HanumanInstitute.SynthMultiViewer.Controls;
using HanumanInstitute.SynthMultiViewer.ViewModels;
using HanumanInstitute.SynthMultiViewer.Views;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class ScrollBarThemeTests
{
    private static HeadlessUnitTestSession UiSession =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestApplication).Assembly);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Editor_Overflow_ShowsReservedAlwaysVisibleBars(bool dark) => UiSession.Dispatch(() =>
    {
        var view = new EditorView
        {
            DataContext = new EditorViewModel
            {
                Script = string.Join('\n', Enumerable.Repeat(new string('x', 400), 80))
            }
        };
        var window = new Window
        {
            Content = view,
            Width = 320,
            Height = 240,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        using var shown = TestSupport.Show(window);
        Dispatcher.UIThread.RunJobs();

        var editor = view.FindControl<BindableTextEditor>("Editor")!;
        AssertAlwaysVisible(editor.GetVisualDescendants().OfType<ScrollViewer>().First(), window);
        return true;
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Viewer_Zoomed_ShowsReservedAlwaysVisibleBars(bool dark) => UiSession.Dispatch(() =>
    {
        using var fixture = new ZoomedViewer(dark);
        AssertAlwaysVisible(fixture.Viewer, fixture.Window);
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Viewer_Default_DoesNotAutoHide() => UiSession.Dispatch(() =>
    {
        using var fixture = new ZoomedViewer();
        Assert.False(fixture.Viewer.AllowAutoHide);
        Assert.True(fixture.Viewer.GetVisualDescendants().OfType<ScrollBar>()
            .All(bar => bar.IsExpanded && !bar.AllowAutoHide));
        return true;
    }, TestContext.Current.CancellationToken);

    private static void AssertAlwaysVisible(ScrollViewer scroll, Window window)
    {
        Assert.False(scroll.AllowAutoHide);
        var presenter = scroll.GetVisualDescendants().OfType<ScrollContentPresenter>().First();
        Assert.Equal(1, Grid.GetColumnSpan(presenter));
        Assert.Equal(1, Grid.GetRowSpan(presenter));

        Assert.True(Application.Current!.TryGetResource("AppScrollBarThumb", window.ActualThemeVariant, out var thumbBrush));
        var expected = Assert.IsType<SolidColorBrush>(thumbBrush).Color;

        foreach (var orientation in new[] { Orientation.Vertical, Orientation.Horizontal })
        {
            var bar = scroll.GetVisualDescendants().OfType<ScrollBar>()
                .Single(item => item.Orientation == orientation);
            Assert.True(bar.IsVisible);
            Assert.True(bar.IsExpanded);
            Assert.False(bar.AllowAutoHide);
            Assert.Equal(14, orientation == Orientation.Vertical ? bar.Bounds.Width : bar.Bounds.Height);

            var gutter = bar.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_Gutter");
            var track = bar.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_Track");
            var fill = bar.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_ThumbFill");
            Assert.Equal(1, gutter.Opacity);
            Assert.Equal(1, track.Opacity);
            Assert.Equal(1, fill.Opacity);
            Assert.Equal(expected, Assert.IsType<SolidColorBrush>(fill.Background).Color);
            Assert.Null(bar.GetVisualDescendants().OfType<Thumb>().Single().RenderTransform);
        }
    }

    private sealed class ZoomedViewer : IDisposable
    {
        private readonly IDisposable _shown;

        public ZoomedViewer(bool dark = false)
        {
            Viewer = new ZoomViewer
            {
                Child = new Border { Width = 80, Height = 40, Background = Brushes.Gray }
            };
            Window = new Window
            {
                Width = 240,
                Height = 200,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
                Content = new Border { Width = 200, Height = 160, Child = Viewer }
            };
            _shown = TestSupport.Show(Window);
            Viewer.Zoom = 10;
            Dispatcher.UIThread.RunJobs();
        }

        public ZoomViewer Viewer { get; }
        public Window Window { get; }

        public void Dispose() => _shown.Dispose();
    }
}
