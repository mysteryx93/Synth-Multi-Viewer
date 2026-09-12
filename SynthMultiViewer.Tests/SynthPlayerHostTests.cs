using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using HanumanInstitute.ApiAviSynth;
using HanumanInstitute.MediaPlayer.Avalonia;
using HanumanInstitute.MediaSynthUI;
using HanumanInstitute.SynthMultiViewer.ViewModels;
using HanumanInstitute.SynthMultiViewer.Views;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class SynthPlayerHostTests
{
    [AvaloniaFact]
    public void MeasureOverride_HostInWindow_ArrangesHostContainer()
    {
        var host = new SynthPlayerHost();

        using var window = TestSupport.Show(new Window { Width = 320, Height = 240, Content = host });

        Assert.True(host.Bounds.Width > 0);
        Assert.True(host.Bounds.Height > 0);
        Assert.Equal(host.Bounds.Size, host.HostContainer.Bounds.Size);
    }

    [AvaloniaFact]
    public void ViewerHost_MediaPlayerLayout_GivesHostAVideoSlot()
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

        Assert.True(host.Bounds.Height > 50);
        Assert.True(host.HostContainer.Bounds.Height > 50);
    }

    [AvaloniaFact]
    public void ApplyZoomMode_DefaultZoom_ShowsZoomViewer()
    {
        var host = new SynthPlayerHost();

        using var window = TestSupport.Show(new Window { Width = 320, Height = 240, Content = host });
        var grid = Assert.IsType<Grid>(host.HostContainer);

        Assert.False(grid.Children.OfType<Image>().Single().IsVisible);
        Assert.True(grid.Children.OfType<ZoomViewer>().Single().IsVisible);
    }

    [AvaloniaFact]
    public void PresentTestFrame_ColoredBitmap_SizesVisibleImage()
    {
        var host = new SynthPlayerHost();
        using var window = TestSupport.Show(new Window { Width = 320, Height = 240, Content = host });
        var bitmap = new WriteableBitmap(
            new PixelSize(64, 48), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var framebuffer = bitmap.Lock())
        {
            var color = unchecked((int)0xFFC06020);
            for (var y = 0; y < 48; y++)
            {
                for (var x = 0; x < 64; x++)
                {
                    System.Runtime.InteropServices.Marshal.WriteInt32(
                        framebuffer.Address, y * framebuffer.RowBytes + x * 4, color);
                }
            }
        }

        host.PresentTestFrame(bitmap);
        Dispatcher.UIThread.RunJobs();

        var grid = Assert.IsType<Grid>(host.HostContainer);
        var zoom = grid.Children.OfType<ZoomViewer>().Single();
        var sizer = Assert.IsType<Border>(Assert.IsType<Grid>(zoom.Content).Children.Single());
        Assert.True(zoom.IsVisible);
        Assert.True(host.HostContainer.IsVisible);
        Assert.Equal(bitmap, zoom.Child is Image image ? image.Source : null);
        Assert.Equal(64, sizer.Width);
        Assert.Equal(48, sizer.Height);
    }

    [AvaloniaFact]
    public void ApplyZoomMode_ScaleToFit_ShowsUniformImage()
    {
        var host = new SynthPlayerHost { ZoomScaleToFit = true };
        using var window = TestSupport.Show(new Window { Width = 320, Height = 240, Content = host });
        var bitmap = new WriteableBitmap(
            new PixelSize(64, 48), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        host.PresentTestFrame(bitmap);
        Dispatcher.UIThread.RunJobs();

        var grid = Assert.IsType<Grid>(host.HostContainer);
        var image = grid.Children.OfType<Image>().Single();
        Assert.True(image.IsVisible);
        Assert.False(grid.Children.OfType<ZoomViewer>().Single().IsVisible);
        Assert.Equal(Avalonia.Media.Stretch.Uniform, image.Stretch);
        Assert.True(image.UseLayoutRounding);
        Assert.True(image.Bounds.Width > 64);
        Assert.True(image.Bounds.Height > 48);
    }

    [AvaloniaFact]
    public void Images_Default_SnapToDevicePixels()
    {
        var host = new SynthPlayerHost();

        using var window = TestSupport.Show(new Window { Width = 320, Height = 240, Content = host });
        var grid = Assert.IsType<Grid>(host.HostContainer);
        var zoom = grid.Children.OfType<ZoomViewer>().Single();

        Assert.True(grid.UseLayoutRounding);
        Assert.True(grid.Children.OfType<Image>().Single().UseLayoutRounding);
        Assert.True(zoom.UseLayoutRounding);
        Assert.True(((Image)zoom.Child!).UseLayoutRounding);
    }

    [AvaloniaTheory]
    [InlineData(160, 40, 0.5, 0.05, 1)]
    [InlineData(160, 40, 0.5, 0.95, -1)]
    [InlineData(40, 160, 0.05, 0.5, 1)]
    [InlineData(40, 160, 0.95, 0.5, -1)]
    [InlineData(160, 40, 0.5, 0.5, 1)]
    public void ScaleToFit_WheelAnywhereInViewport_LeavesFitAndContinuesZooming(
        int width, int height, double x, double y, int direction)
    {
        var model = TestSupport.CreateMain();
        var view = new ViewerView { DataContext = new ViewerViewModel() };
        var window = new Window { Width = 640, Height = 480, DataContext = model, Content = view };
        using var shown = TestSupport.Show(window);
        var host = view.FindControl<SynthPlayerHost>("PlayerHost")!;
        using var bitmap = new WriteableBitmap(
            new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        host.PresentTestFrame(bitmap);
        model.Zoom = 0;
        Dispatcher.UIThread.RunJobs();
        var point = host.TranslatePoint(new Point(host.Bounds.Width * x, host.Bounds.Height * y), window)!.Value;

        window.MouseWheel(point, new Vector(0, direction));
        Dispatcher.UIThread.RunJobs();

        Assert.False(host.ZoomScaleToFit);
        Assert.False(model.ZoomScaleToFit);
        Assert.Equal(1, model.Zoom);
        window.MouseWheel(point, new Vector(0, direction));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(direction > 0 ? model.ZoomIncrement : 1 / model.ZoomIncrement, model.Zoom, precision: 8);
    }

    [AvaloniaFact]
    public void ChildImage_SourceAssigned_SizesZoomSurfaceToBitmap()
    {
        var image = new Image { Stretch = Avalonia.Media.Stretch.None };
        var zoom = new ZoomViewer { Child = image };
        using var window = TestSupport.Show(new Window { Width = 400, Height = 300, Content = zoom });
        using var bitmap = new WriteableBitmap(
            new PixelSize(80, 40), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

        image.Source = bitmap;
        Dispatcher.UIThread.RunJobs();

        var sizer = Assert.IsType<Border>(Assert.IsType<Grid>(zoom.Content).Children.Single());
        Assert.Equal(80, sizer.Width);
        Assert.Equal(40, sizer.Height);
    }

    [AvaloniaFact]
    public void Zoom_ZeroWithMinZoom_KeepsScaleToFit()
    {
        var model = TestSupport.CreateMain();
        var view = new ViewerView { DataContext = new ViewerViewModel() };
        using var window = TestSupport.Show(new Window
        {
            Width = 640,
            Height = 480,
            DataContext = model,
            Content = view
        });
        var host = view.FindControl<SynthPlayerHost>("PlayerHost")!;

        model.Zoom = 0;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, model.Zoom);
        Assert.True(model.ZoomScaleToFit);
        Assert.True(host.ZoomScaleToFit);
        Assert.Equal(0, host.Zoom);
        Assert.NotEqual(0.1, host.Zoom);
    }

    [AvaloniaFact]
    public void Zoom_LargerThanViewport_CentersContent()
    {
        var image = new Image { Stretch = Avalonia.Media.Stretch.None };
        var zoom = new ZoomViewer { Child = image, MinZoom = 0.1, MaxZoom = 10 };
        using var window = TestSupport.Show(new Window
        {
            Width = 240,
            Height = 200,
            Content = new Border { Width = 200, Height = 160, Child = zoom }
        });
        using var bitmap = new WriteableBitmap(
            new PixelSize(80, 40), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        image.Source = bitmap;
        Dispatcher.UIThread.RunJobs();

        zoom.Zoom = 4;
        Dispatcher.UIThread.RunJobs();

        var sizer = Assert.IsType<Border>(Assert.IsType<Grid>(zoom.Content).Children.Single());
        Assert.Equal(320, sizer.Width);
        Assert.Equal(160, sizer.Height);
        Assert.True(zoom.Extent.Width > zoom.Viewport.Width);
        Assert.True(zoom.Offset.X > 0);
        Assert.InRange(zoom.Offset.X, (320 - zoom.Viewport.Width) / 2 - 2, (320 - zoom.Viewport.Width) / 2 + 2);
    }

    [AvaloniaFact]
    public void PointerDrag_WhenZoomed_PansContent()
    {
        var image = new Image { Stretch = Avalonia.Media.Stretch.None };
        var zoom = new ZoomViewer { Child = image, MinZoom = 0.1, MaxZoom = 10 };
        var window = new Window
        {
            Width = 240,
            Height = 200,
            Content = new Border { Width = 200, Height = 160, Child = zoom }
        };
        using var _ = TestSupport.Show(window);
        using var bitmap = new WriteableBitmap(
            new PixelSize(80, 40), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        image.Source = bitmap;
        zoom.Zoom = 4;
        Dispatcher.UIThread.RunJobs();
        var start = zoom.Offset;

        var origin = zoom.TranslatePoint(new Point(40, 40), window)!.Value;
        window.MouseMove(origin);
        window.MouseDown(origin, Avalonia.Input.MouseButton.Left);
        window.MouseMove(origin + new Vector(-30, -20));
        window.MouseUp(origin + new Vector(-30, -20), Avalonia.Input.MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.True(zoom.Offset.X > start.X);
        Assert.True(zoom.Offset.Y >= start.Y);
    }

    [AvaloniaFact]
    public void Script_AviSynthPaused_PresentsFirstFrame()
    {
        SkipIfAviSynthUnavailable();
        var host = new SynthPlayerHost { Kind = ScriptKind.AviSynth };
        using var window = TestSupport.Show(new Window { Width = 320, Height = 240, Content = host });

        host.Script = "BlankClip(length=12, width=160, height=120, pixel_type=\"RGB24\")\n";
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(host.VideoSource);
        Assert.Equal(TimeSpan.Zero, host.Position);
        Assert.Equal(new PixelSize(160, 120), host.VideoSource.PixelSize);
    }

    [AvaloniaFact]
    public void Position_AviSynthSeekWhilePaused_PresentsRequestedFrame()
    {
        SkipIfAviSynthUnavailable();
        var host = new SynthPlayerHost { Kind = ScriptKind.AviSynth };
        using var window = TestSupport.Show(new Window { Width = 320, Height = 240, Content = host });
        host.Script = "BlankClip(length=12, width=160, height=120, pixel_type=\"RGB24\")\n";
        Dispatcher.UIThread.RunJobs();

        host.Position = TimeSpan.FromSeconds(5);
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(host.VideoSource);
        Assert.Equal(TimeSpan.FromSeconds(5), host.Position);
    }

    private static void SkipIfAviSynthUnavailable() =>
        Assert.SkipUnless(AvsScript.TryFindLibrary(out _), "AviSynth+ native library was not found.");
}
