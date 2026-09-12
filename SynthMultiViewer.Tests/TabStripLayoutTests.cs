using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HanumanInstitute.SynthMultiViewer.Views;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class TabStripLayoutTests
{
    [AvaloniaFact]
    public async Task SelectedPipe_TabShown_SitsBelowHeaderContent()
    {
        var model = TestSupport.CreateMain();
        await model.New.Execute();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        Dispatcher.UIThread.RunJobs();

        var item = view.GetVisualDescendants().OfType<TabStripItem>().First();
        var pipe = item.GetVisualDescendants().OfType<Border>()
            .Single(x => x.Name == "PART_SelectedPipe" && ReferenceEquals(x.TemplatedParent, item));
        var presenter = item.GetVisualDescendants().OfType<ContentPresenter>()
            .Single(x => x.Name == "PART_ContentPresenter" && ReferenceEquals(x.TemplatedParent, item));
        var pipeTop = pipe.TranslatePoint(new Point(0, 0), item)!.Value.Y;
        var contentTop = presenter.TranslatePoint(new Point(0, 0), item)!.Value.Y;
        var contentBottom = presenter.TranslatePoint(new Point(0, presenter.Bounds.Height), item)!.Value.Y;

        Assert.True(pipe.IsVisible);
        Assert.True(pipeTop >= contentBottom - 1);
        Assert.InRange(item.Bounds.Height - (pipeTop + pipe.Bounds.Height), 0, 6);
        Assert.True(pipe.Bounds.Height >= 2);
        Assert.InRange(item.Bounds.Height, 36, 44);
        var contentCenter = contentTop + presenter.Bounds.Height / 2;
        Assert.InRange(contentCenter, item.Bounds.Height * 0.3, item.Bounds.Height * 0.7);
    }

    [AvaloniaFact]
    public async Task TabClose_Shown_CentersGlyphInsideButton()
    {
        var model = TestSupport.CreateMain();
        await model.New.Execute();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        Dispatcher.UIThread.RunJobs();
        var close = view.GetVisualDescendants().OfType<Button>().First(button => button.Classes.Contains("tab-close"));
        var glyph = close.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single();
        var origin = glyph.TranslatePoint(new Point(0, 0), close)!.Value;
        var center = origin + new Vector(glyph.Bounds.Width / 2, glyph.Bounds.Height / 2);

        Assert.Equal(16, close.Bounds.Width);
        Assert.Equal(16, close.Bounds.Height);
        Assert.Equal(8, glyph.Bounds.Width);
        Assert.Equal(8, glyph.Bounds.Height);
        Assert.InRange(center.X, close.Bounds.Width / 2 - 1, close.Bounds.Width / 2 + 1);
        Assert.InRange(center.Y, close.Bounds.Height / 2 - 1, close.Bounds.Height / 2 + 1);
    }
}
