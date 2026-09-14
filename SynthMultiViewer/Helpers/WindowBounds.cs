using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace HanumanInstitute.SynthMultiViewer.Helpers;

/// <summary>
/// Two-way binds a window's restored size and maximized state.
/// Size is captured only while the window is restored so maximize does not overwrite it.
/// </summary>
public static class WindowBounds
{
    /// <summary>
    /// Defines whether the window size and maximized state are tracked.
    /// </summary>
    public static readonly AttachedProperty<bool> TrackProperty =
        AvaloniaProperty.RegisterAttached<Window, bool>("Track", typeof(WindowBounds));

    /// <summary>
    /// Defines the restored window width.
    /// </summary>
    public static readonly AttachedProperty<double> WidthProperty =
        AvaloniaProperty.RegisterAttached<Window, double>("Width", typeof(WindowBounds));

    /// <summary>
    /// Defines the restored window height.
    /// </summary>
    public static readonly AttachedProperty<double> HeightProperty =
        AvaloniaProperty.RegisterAttached<Window, double>("Height", typeof(WindowBounds));

    /// <summary>
    /// Defines whether the window is maximized.
    /// </summary>
    public static readonly AttachedProperty<bool> MaximizedProperty =
        AvaloniaProperty.RegisterAttached<Window, bool>("Maximized", typeof(WindowBounds));

    static WindowBounds()
    {
        TrackProperty.Changed.AddClassHandler<Window, bool>(OnTrackChanged);
        WidthProperty.Changed.AddClassHandler<Window, double>(OnWidthChanged);
        HeightProperty.Changed.AddClassHandler<Window, double>(OnHeightChanged);
        MaximizedProperty.Changed.AddClassHandler<Window, bool>(OnMaximizedChanged);
    }

    /// <summary>
    /// Gets whether the window size and maximized state are tracked.
    /// </summary>
    public static bool GetTrack(AvaloniaObject d) => d.GetValue(TrackProperty);

    /// <summary>
    /// Sets whether the window size and maximized state are tracked.
    /// </summary>
    public static void SetTrack(AvaloniaObject d, bool value) => d.SetValue(TrackProperty, value);

    /// <summary>
    /// Gets the restored window width.
    /// </summary>
    public static double GetWidth(AvaloniaObject d) => d.GetValue(WidthProperty);

    /// <summary>
    /// Sets the restored window width.
    /// </summary>
    public static void SetWidth(AvaloniaObject d, double value) => d.SetValue(WidthProperty, value);

    /// <summary>
    /// Gets the restored window height.
    /// </summary>
    public static double GetHeight(AvaloniaObject d) => d.GetValue(HeightProperty);

    /// <summary>
    /// Sets the restored window height.
    /// </summary>
    public static void SetHeight(AvaloniaObject d, double value) => d.SetValue(HeightProperty, value);

    /// <summary>
    /// Gets whether the window is maximized.
    /// </summary>
    public static bool GetMaximized(AvaloniaObject d) => d.GetValue(MaximizedProperty);

    /// <summary>
    /// Sets whether the window is maximized.
    /// </summary>
    public static void SetMaximized(AvaloniaObject d, bool value) => d.SetValue(MaximizedProperty, value);

    private static void OnTrackChanged(Window window, AvaloniaPropertyChangedEventArgs<bool> e)
    {
        if (Design.IsDesignMode || !e.NewValue.Value)
        {
            return;
        }

        window.GetObservable(Window.WindowStateProperty).Subscribe(state =>
        {
            SetMaximized(window, state == WindowState.Maximized);
            CaptureSize(window);
        });
        window.GetObservable(Layoutable.WidthProperty).Subscribe(_ => CaptureSize(window));
        window.GetObservable(Layoutable.HeightProperty).Subscribe(_ => CaptureSize(window));
    }

    private static void CaptureSize(Window window)
    {
        if (window.WindowState != WindowState.Normal)
        {
            return;
        }

        if (double.IsFinite(window.Width) && window.Width > 0)
        {
            SetWidth(window, window.Width);
        }

        if (double.IsFinite(window.Height) && window.Height > 0)
        {
            SetHeight(window, window.Height);
        }
    }

    private static void OnWidthChanged(Window window, AvaloniaPropertyChangedEventArgs<double> e)
    {
        if (window.WindowState == WindowState.Normal && double.IsFinite(e.NewValue.Value) && e.NewValue.Value > 0)
        {
            window.Width = e.NewValue.Value;
        }
    }

    private static void OnHeightChanged(Window window, AvaloniaPropertyChangedEventArgs<double> e)
    {
        if (window.WindowState == WindowState.Normal && double.IsFinite(e.NewValue.Value) && e.NewValue.Value > 0)
        {
            window.Height = e.NewValue.Value;
        }
    }

    private static readonly AttachedProperty<bool> ConcealUntilShownProperty =
        AvaloniaProperty.RegisterAttached<Window, bool>("ConcealUntilShown", typeof(WindowBounds));

    private static void OnMaximizedChanged(Window window, AvaloniaPropertyChangedEventArgs<bool> e)
    {
        window.WindowState = e.NewValue.Value ? WindowState.Maximized : WindowState.Normal;
        if (e.NewValue.Value && !window.IsVisible)
        {
            ConcealUntilShown(window);
        }
    }

    private static void ConcealUntilShown(Window window)
    {
        if (window.GetValue(ConcealUntilShownProperty))
        {
            return;
        }

        window.SetValue(ConcealUntilShownProperty, true);
        window.Opacity = 0;
        window.Opened += OnConcealedOpened;
    }

    private static void OnConcealedOpened(object? sender, EventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        window.Opened -= OnConcealedOpened;
        if (GetMaximized(window))
        {
            window.WindowState = WindowState.Maximized;
        }

        Dispatcher.UIThread.Post(() => window.Opacity = 1, DispatcherPriority.Loaded);
    }
}
