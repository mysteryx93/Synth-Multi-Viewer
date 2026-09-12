using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace HanumanInstitute.MediaSynthUI;

/// <summary>
/// Scaled canvas that hosts the zoomed control inside <see cref="ZoomViewer"/>.
/// </summary>
internal sealed class ZoomSurface
{
    private readonly ScaleTransform _scale = new(1, 1);
    private Control? _child;

    public ZoomSurface()
    {
        Canvas.RenderTransform = _scale;
        Canvas.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
        Sizer.Child = Canvas;
        Root.Children.Add(Sizer);
    }

    public Grid Root { get; } = new() { Background = Brushes.Transparent, UseLayoutRounding = true };

    public Border Sizer { get; } = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        ClipToBounds = true,
        UseLayoutRounding = true
    };

    public Canvas Canvas { get; } = new()
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        UseLayoutRounding = true
    };

    public event EventHandler? ContentSizeChanged;

    public Control? Child
    {
        get => _child;
        set
        {
            if (_child != null)
            {
                Canvas.Children.Remove(_child);
            }

            _child = value;
            if (value != null)
            {
                Avalonia.Controls.Canvas.SetLeft(value, 0);
                Avalonia.Controls.Canvas.SetTop(value, 0);
                Canvas.Children.Add(value);
                if (value is Image image)
                {
                    image.GetObservable(Image.SourceProperty).Subscribe(_ =>
                        ContentSizeChanged?.Invoke(this, EventArgs.Empty));
                }
            }

            ContentSizeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public Size ContentSize
    {
        get
        {
            if (_child is Image { Source: { } source })
            {
                return source.Size;
            }

            return new Size(_child?.DesiredSize.Width ?? 0, _child?.DesiredSize.Height ?? 0);
        }
    }

    public void ApplyScale(double zoom)
    {
        var content = ContentSize;
        Canvas.Width = Math.Max(0, content.Width);
        Canvas.Height = Math.Max(0, content.Height);
        Sizer.Width = Math.Max(0, content.Width * zoom);
        Sizer.Height = Math.Max(0, content.Height * zoom);
        _scale.ScaleX = zoom;
        _scale.ScaleY = zoom;
        Sizer.InvalidateMeasure();
    }

    public void FitViewport(Size viewport)
    {
        Root.Width = Math.Max(viewport.Width, Sizer.Width);
        Root.Height = Math.Max(viewport.Height, Sizer.Height);
    }
}
