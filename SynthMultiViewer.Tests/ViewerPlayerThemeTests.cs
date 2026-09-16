using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaPlayer = HanumanInstitute.MediaPlayer.Avalonia.MediaPlayer;
using HanumanInstitute.SynthMultiViewer.ViewModels;
using HanumanInstitute.SynthMultiViewer.Views;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class ViewerPlayerThemeTests
{
    [AvaloniaFact]
    public void SeekBar_Shown_HasFullHeightHittableLane()
    {
        var (view, player) = ShowViewer();
        using var shown = view;
        WaitForSeekTemplate(player);

        var slider = player.SeekBarPart!;
        var decrease = player.SeekBarDecreasePart!;
        var increase = player.SeekBarIncreasePart!;

        Assert.True(slider.Bounds.Height >= 24);
        Assert.True(decrease.Bounds.Height >= 24);
        Assert.True(increase.Bounds.Height >= 24);
        Assert.Equal(slider.Bounds.Height, decrease.Bounds.Height);
        Assert.Equal(slider.Bounds.Height, increase.Bounds.Height);
        Assert.Equal(0, decrease.MinWidth);
        Assert.Equal(0, increase.MinWidth);
        Assert.Equal(0, player.SeekBarThumbPart!.MinWidth);
        Assert.DoesNotContain(slider.GetVisualDescendants(), visual => visual.Name == "TrackBackground");
        Assert.True(decrease.IsHitTestVisible);
        Assert.True(increase.IsHitTestVisible);
    }

    [AvaloniaFact]
    public void Transport_Shown_UsesSolidFillWithoutGradient()
    {
        var (view, player) = ShowViewer();
        using var shown = view;

        var transport = player.GetVisualDescendants().OfType<Border>()
            .Single(border => border.Name == "PART_Transport");

        Assert.IsType<SolidColorBrush>(transport.Background);
        Assert.Equal(36, transport.Bounds.Height);

        var position = player.GetVisualDescendants().OfType<Label>().Single(label => label.Name == "PART_Position");
        Assert.Equal(88, position.Width);
        Assert.Equal(VerticalAlignment.Center, position.VerticalAlignment);
    }

    private static (IDisposable Shown, AvaloniaPlayer Player) ShowViewer()
    {
        var view = new ViewerView { DataContext = new ViewerViewModel() };
        var window = new Window
        {
            Width = 640,
            Height = 480,
            DataContext = TestSupport.CreateMain(),
            Content = view
        };
        var shown = TestSupport.Show(window);
        var player = view.FindControl<AvaloniaPlayer>("Player")!;
        return (shown, player);
    }

    private static void WaitForSeekTemplate(AvaloniaPlayer player)
    {
        Dispatcher.UIThread.RunJobs();
        player.SeekBarPart?.ApplyTemplate();
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(player.SeekBarDecreasePart);
    }
}
