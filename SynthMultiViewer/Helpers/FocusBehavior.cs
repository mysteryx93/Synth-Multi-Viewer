using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace HanumanInstitute.SynthMultiViewer.Helpers;

/// <summary>
/// Focuses a control when it appears, selecting text in text boxes.
/// </summary>
public static class FocusBehavior
{
    /// <summary>
    /// Defines whether a control takes focus when it becomes visible.
    /// </summary>
    public static readonly AttachedProperty<bool> WhenVisibleProperty = AvaloniaProperty.RegisterAttached<Control, bool>("WhenVisible", typeof(FocusBehavior));

    static FocusBehavior()
    {
        WhenVisibleProperty.Changed.AddClassHandler<Control>((control, change) =>
        {
            control.Loaded -= OnLoaded;
            control.PropertyChanged -= OnPropertyChanged;
            if (change.NewValue is true)
            {
                control.Loaded += OnLoaded;
                control.PropertyChanged += OnPropertyChanged;
                QueueFocus(control);
            }
        });
    }

    /// <summary>
    /// Gets whether the control takes focus when shown.
    /// </summary>
    public static bool GetWhenVisible(Control control) => control.GetValue(WhenVisibleProperty);

    /// <summary>
    /// Sets whether the control takes focus when shown.
    /// </summary>
    public static void SetWhenVisible(Control control, bool value) => control.SetValue(WhenVisibleProperty, value);

    private static void OnLoaded(object? sender, RoutedEventArgs e) => QueueFocus((Control)sender!);

    private static void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.IsVisibleProperty && e.NewValue is true)
        {
            QueueFocus((Control)sender!);
        }
    }

    private static void QueueFocus(Control control)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (GetWhenVisible(control) && control.IsLoaded && control.IsEffectivelyVisible && control.Focus())
            {
                (control as TextBox)?.SelectAll();
            }
        }, DispatcherPriority.Input);
    }
}
