using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using HanumanInstitute.MediaPlayer.Avalonia;
using HanumanInstitute.MediaSynthUI;
using HanumanInstitute.SynthMultiViewer.ViewModels;
using HanumanInstitute.SynthMultiViewer.Views;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class ViewerBindingTests
{
    [AvaloniaFact]
    public void MediaLoaded_BackgroundViewerLoads_RestoresOnlyItsPosition()
    {
        var model = TestSupport.CreateMain();
        var first = new ViewerViewModel();
        var second = new ViewerViewModel();
        model.ScriptList.Add(first);
        model.ScriptList.Add(second);
        model.SelectedItem = first;
        first.Position = TimeSpan.FromSeconds(20);
        model.SelectedItem = second;
        second.Position = TimeSpan.FromSeconds(40);
        var view = new ViewerView { DataContext = first };
        using var window = TestSupport.Show(new Window { DataContext = model, Content = view });
        var host = view.FindControl<SynthPlayerHost>("PlayerHost")!;
        host.SetValue(PlayerHostBase.DurationProperty, TimeSpan.FromSeconds(100));

        host.RaiseEvent(new RoutedEventArgs(PlayerHostBase.MediaLoadedEvent));

        Assert.Equal(TimeSpan.FromSeconds(20), first.Position);
        Assert.Equal(TimeSpan.FromSeconds(40), second.Position);
    }

    [AvaloniaFact]
    public void ViewerBindings_PlayerStateChanges_UpdatesOwningModel()
    {
        var first = new ViewerViewModel();
        var second = new ViewerViewModel();
        var view = new ViewerView { DataContext = first };
        using var window = TestSupport.Show(new Window { DataContext = TestSupport.CreateMain(), Content = view });
        var host = view.FindControl<SynthPlayerHost>("PlayerHost")!;

        host.SetValue(PlayerHostBase.DurationProperty, TimeSpan.FromSeconds(100));
        host.DisplayError("test error");

        Assert.Equal(host.Duration, first.Duration);
        Assert.Equal("test error", first.ErrorMessage);
        Assert.Null(second.ErrorMessage);
    }

    [AvaloniaFact]
    public void ViewerBindings_DataContextReplaced_PublishesCurrentPlayerState()
    {
        var view = new ViewerView { DataContext = new ViewerViewModel() };
        using var window = TestSupport.Show(new Window { DataContext = TestSupport.CreateMain(), Content = view });
        var host = view.FindControl<SynthPlayerHost>("PlayerHost")!;
        host.SetValue(PlayerHostBase.DurationProperty, TimeSpan.FromSeconds(100));
        host.DisplayError("test error");
        var replacement = new ViewerViewModel();

        view.DataContext = replacement;

        Assert.Equal(host.Duration, replacement.Duration);
        Assert.Equal("test error", replacement.ErrorMessage);
    }

    [AvaloniaFact]
    public void ViewerBindings_KindSet_AssignsHostKind()
    {
        var model = new ViewerViewModel { Kind = ScriptKind.AviSynth };
        var view = new ViewerView { DataContext = model };
        using var window = TestSupport.Show(new Window { DataContext = TestSupport.CreateMain(), Content = view });
        var host = view.FindControl<SynthPlayerHost>("PlayerHost")!;

        Assert.Equal(ScriptKind.AviSynth, host.Kind);
    }

    [AvaloniaFact]
    public void ViewerBindings_FileNameSet_AssignsHostPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "clip.vpy");
        var model = new ViewerViewModel { FileName = path };
        var view = new ViewerView { DataContext = model };
        using var window = TestSupport.Show(new Window { DataContext = TestSupport.CreateMain(), Content = view });
        var host = view.FindControl<SynthPlayerHost>("PlayerHost")!;

        Assert.Equal(path, host.Path);
    }
}
