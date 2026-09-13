using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using Avalonia.Xaml.Interactions.Draggable;
using HanumanInstitute.MediaSynthUI;
using HanumanInstitute.SynthMultiViewer.Models;
using HanumanInstitute.SynthMultiViewer.ViewModels;
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

    [AvaloniaFact]
    public async Task TabStripItem_HasHorizontalItemDragBehavior()
    {
        var model = TestSupport.CreateMain();
        await model.New.Execute();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        Dispatcher.UIThread.RunJobs();

        var item = view.GetVisualDescendants().OfType<TabStripItem>().First();
        var drag = Assert.IsType<ItemDragBehavior>(Assert.Single(Interaction.GetBehaviors(item)));

        Assert.Equal(Avalonia.Layout.Orientation.Horizontal, drag.Orientation);
    }

    [AvaloniaFact]
    public async Task DragTab_AcrossNeighbor_ReordersScriptList()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        var first = model.SelectedItem;
        await model.New.Execute();
        var second = model.SelectedItem;
        Dispatcher.UIThread.RunJobs();
        var items = view.GetVisualDescendants().OfType<TabStripItem>().ToList();
        var start = items[0].TranslatePoint(
            new Point(items[0].Bounds.Width / 2, items[0].Bounds.Height / 2), view)!.Value;
        var end = items[1].TranslatePoint(
            new Point(items[1].Bounds.Width / 2, items[1].Bounds.Height / 2), view)!.Value;

        view.MouseDown(start, MouseButton.Left);
        view.MouseMove(end + new Vector(8, 0));
        view.MouseUp(end + new Vector(8, 0), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Same(second, model.ScriptList[0]);
        Assert.Same(first, model.ScriptList[1]);
    }

    [AvaloniaFact]
    public async Task AltRight_MovesSelectedTabAndKeepsItSelected()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        var first = model.SelectedItem;
        await model.New.Execute();
        model.SelectedItem = first;
        Dispatcher.UIThread.RunJobs();
        view.GetVisualDescendants().OfType<TabStripItem>().First().Focus();
        Dispatcher.UIThread.RunJobs();

        TestSupport.Press(view, Key.Right, RawInputModifiers.Alt);
        Dispatcher.UIThread.RunJobs();

        var strip = view.GetVisualDescendants().OfType<TabStrip>().First();
        Assert.Same(first, model.ScriptList[1]);
        Assert.Same(first, model.SelectedItem);
        Assert.Same(first, strip.SelectedItem);
        Assert.True(first!.IsActive);
    }

    [AvaloniaFact]
    public async Task TabTint_Shown_FillsItemAndUsesMixedHue()
    {
        var model = TestSupport.CreateMain();
        await model.New.Execute();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        Dispatcher.UIThread.RunJobs();

        var item = view.GetVisualDescendants().OfType<TabStripItem>().First();
        var tint = item.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "PART_Tint");
        var origin = tint.TranslatePoint(default, item)!.Value;

        Assert.Equal(TabColors.For(ScriptKind.VapourSynth, false, AppTheme.Light),
            Assert.IsType<SolidColorBrush>(tint.Background).Color);
        Assert.InRange(origin.X, -0.5, 0.5);
        Assert.InRange(origin.Y, -0.5, 0.5);
        Assert.InRange(tint.Bounds.Width, item.Bounds.Width - 1, item.Bounds.Width + 1);
        Assert.InRange(tint.Bounds.Height, item.Bounds.Height - 1, item.Bounds.Height + 1);
    }

    [AvaloniaFact]
    public async Task AdjacentTabs_Shown_SitFlush()
    {
        var model = TestSupport.CreateMain();
        var view = new MainView { DataContext = model };
        using var window = TestSupport.Show(view);
        await model.New.Execute();
        await model.New.Execute();
        Dispatcher.UIThread.RunJobs();

        var items = view.GetVisualDescendants().OfType<TabStripItem>().ToList();
        var firstRight = items[0].TranslatePoint(new Point(items[0].Bounds.Width, 0), view)!.Value.X;
        var secondLeft = items[1].TranslatePoint(default, view)!.Value.X;

        Assert.InRange(secondLeft - firstRight, 0, 1);
    }
}
