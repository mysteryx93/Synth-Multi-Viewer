using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HanumanInstitute.MediaPlayer.Avalonia;
using HanumanInstitute.MediaSynthUI;
using HanumanInstitute.SynthMultiViewer.ViewModels;
using HanumanInstitute.SynthMultiViewer.Views;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class ViewerPositionTests
{
    [AvaloniaFact]
    public void PositionDisplay_MediaLoadedAtZero_ShowsOneBasedCount()
    {
        var view = new ViewerView { DataContext = new ViewerViewModel() };
        using var window = TestSupport.Show(new Window
        {
            Width = 640,
            Height = 480,
            DataContext = TestSupport.CreateMain(),
            Content = view
        });
        var host = view.FindControl<SynthPlayerHost>("PlayerHost")!;
        host.SetValue(PlayerHostBase.DurationProperty, TimeSpan.FromSeconds(239));
        host.SetValue(PlayerHostBase.IsMediaLoadedProperty, true);
        host.SetValue(PlayerHostBase.PositionProperty, TimeSpan.FromSeconds(1));
        host.SetValue(PlayerHostBase.PositionProperty, TimeSpan.Zero);
        Dispatcher.UIThread.RunJobs();

        var player = view.FindControl<HanumanInstitute.MediaPlayer.Avalonia.MediaPlayer>("Player")!;
        var label = player.GetVisualDescendants().OfType<Label>().Single();

        Assert.Equal("1 / 240", label.Content);
    }
}
