using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HanumanInstitute.SynthMultiViewer.Views;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class ComboBoxCaptureScratch
{
    [AvaloniaFact]
    public void CaptureVariants()
    {
        Capture("default", null);
        Capture("center-tall", ComboStyle(32, new(12, 0, 0, 0)));
        Capture("compact", ComboStyle(28, new(10, 0, 0, 0)));
        Capture("compact-8", ComboStyle(28, new(8, 0, 0, 0)));
    }

    private static void Capture(string name, Style? style)
    {
        var view = new SettingsView { DataContext = TestSupport.CreateSettings() };
        if (style != null)
        {
            view.Styles.Add(style);
        }

        using var _ = TestSupport.Show(view);
        Dispatcher.UIThread.RunJobs();
        var combo = view.GetVisualDescendants().OfType<ComboBox>().First();
        var origin = combo.TranslatePoint(new(-80, -8), view)!.Value;
        var bmp = view.CaptureRenderedFrame()!;
        Crop(bmp, origin, 280, 80).Save($"/tmp/combo-{name}.png");
    }

    private static Style ComboStyle(double height, Thickness padding) => new(x => x.OfType<ComboBox>())
    {
        Setters =
        {
            new Setter(Layoutable.MinHeightProperty, height),
            new Setter(Layoutable.HeightProperty, height),
            new Setter(Decorator.PaddingProperty, padding),
            new Setter(ContentControl.VerticalContentAlignmentProperty, VerticalAlignment.Center)
        }
    };

    private static WriteableBitmap Crop(Bitmap source, Point origin, int width, int height)
    {
        var x = Math.Max(0, (int)Math.Round(origin.X * source.Dpi.X / 96));
        var y = Math.Max(0, (int)Math.Round(origin.Y * source.Dpi.Y / 96));
        var scale = source.Dpi.X / 96;
        width = (int)(width * scale);
        height = (int)(height * scale);
        var dest = new WriteableBitmap(new PixelSize(width, height), source.Dpi, source.Format);
        using var fb = dest.Lock();
        source.CopyPixels(new PixelRect(x, y, width, height), fb.Address, fb.RowBytes * height, fb.RowBytes);
        return dest;
    }
}
