using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace HanumanInstitute.SynthMultiViewer.Helpers;

/// <summary>
/// Captures clicks outside a control so it can commit when the target is not focusable.
/// </summary>
public static class MouseCapture
{
    /// <summary>
    /// Defines whether outside pointer presses release the control.
    /// </summary>
    public static readonly AttachedProperty<bool> HasCaptureProperty = AvaloniaProperty.RegisterAttached<Control, bool>("HasCapture", typeof(MouseCapture));

    static MouseCapture()
    {
        HasCaptureProperty.Changed.AddClassHandler<Control>((control, change) =>
        {
            control.AttachedToVisualTree -= OnAttached;
            if (change.NewValue is true)
            {
                control.AttachedToVisualTree += OnAttached;
                Hook(control);
            }
        });
    }

    /// <summary>
    /// Gets whether outside clicks release the control.
    /// </summary>
    public static bool GetHasCapture(Control control) => control.GetValue(HasCaptureProperty);

    /// <summary>
    /// Sets whether outside clicks release the control.
    /// </summary>
    public static void SetHasCapture(Control control, bool value) => control.SetValue(HasCaptureProperty, value);

    private static void OnAttached(object? sender, VisualTreeAttachmentEventArgs e) => Hook((Control)sender!);

    private static void Hook(Control control)
    {
        var top = TopLevel.GetTopLevel(control);
        if (top == null) { return; }

        top.RemoveHandler(InputElement.PointerPressedEvent, OnTopLevelPressed);
        top.AddHandler(InputElement.PointerPressedEvent, OnTopLevelPressed, RoutingStrategies.Tunnel);
    }

    private static void OnTopLevelPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Visual root) { return; }

        var capturing = root.GetVisualDescendants().OfType<Control>().FirstOrDefault(GetHasCapture);
        if (capturing == null) { return; }

        if (e.Source is Visual source &&
            (ReferenceEquals(source, capturing) || capturing.IsVisualAncestorOf(source)))
        {
            return;
        }

        capturing.SetCurrentValue(HasCaptureProperty, false);
        if (capturing.IsFocused)
        {
            TopLevel.GetTopLevel(capturing)?.Focus();
        }
    }
}
