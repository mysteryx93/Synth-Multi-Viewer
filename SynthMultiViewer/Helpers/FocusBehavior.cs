using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit;
using HanumanInstitute.SynthMultiViewer.ViewModels;

namespace HanumanInstitute.SynthMultiViewer.Helpers;

/// <summary>
/// Focuses a control when it appears, selecting text in text boxes.
/// </summary>
public static class FocusBehavior
{
    /// <summary>
    /// Defines whether a control takes focus when it becomes visible.
    /// </summary>
    public static readonly AttachedProperty<bool> WhenVisibleProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("WhenVisible", typeof(FocusBehavior));

    private static readonly AttachedProperty<INotifyPropertyChanged?> ActiveSourceProperty =
        AvaloniaProperty.RegisterAttached<Control, INotifyPropertyChanged?>("ActiveSource", typeof(FocusBehavior));

    private static readonly AttachedProperty<PropertyChangedEventHandler?> ActiveHandlerProperty =
        AvaloniaProperty.RegisterAttached<Control, PropertyChangedEventHandler?>("ActiveHandler", typeof(FocusBehavior));

    static FocusBehavior()
    {
        WhenVisibleProperty.Changed.AddClassHandler<Control>((control, change) =>
        {
            control.Loaded -= OnLoaded;
            control.PropertyChanged -= OnPropertyChanged;
            control.DataContextChanged -= OnDataContextChanged;
            UnhookActive(control);
            if (change.NewValue is true)
            {
                control.Loaded += OnLoaded;
                control.PropertyChanged += OnPropertyChanged;
                control.DataContextChanged += OnDataContextChanged;
                HookActive(control);
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

    private static void OnLoaded(object? sender, RoutedEventArgs e)
    {
        var control = (Control)sender!;
        HookActive(control);
        QueueFocus(control);
    }

    private static void OnDataContextChanged(object? sender, EventArgs e)
    {
        var control = (Control)sender!;
        HookActive(control);
        QueueFocus(control);
    }

    private static void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.IsVisibleProperty && e.NewValue is true)
        {
            QueueFocus((Control)sender!);
        }
    }

    private static void HookActive(Control control)
    {
        UnhookActive(control);
        if (control.DataContext is not INotifyPropertyChanged source)
        {
            return;
        }

        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (e.PropertyName == nameof(IScriptViewModel.IsActive))
            {
                QueueFocus(control);
            }
        };
        source.PropertyChanged += handler;
        control.SetValue(ActiveSourceProperty, source);
        control.SetValue(ActiveHandlerProperty, handler);
    }

    private static void UnhookActive(Control control)
    {
        var source = control.GetValue(ActiveSourceProperty);
        var handler = control.GetValue(ActiveHandlerProperty);
        if (source != null && handler != null)
        {
            source.PropertyChanged -= handler;
        }

        control.ClearValue(ActiveSourceProperty);
        control.ClearValue(ActiveHandlerProperty);
    }

    private static void QueueFocus(Control control) =>
        Dispatcher.UIThread.Post(() => TryFocus(control), DispatcherPriority.Background);

    private static void TryFocus(Control control)
    {
        if (!GetWhenVisible(control) || !control.IsLoaded || !control.IsEffectivelyVisible)
        {
            return;
        }

        if (control.DataContext is IScriptViewModel { IsActive: false })
        {
            return;
        }

        if (control is TextEditor editor)
        {
            editor.TextArea.Focus();
            return;
        }

        if (control.Focus())
        {
            (control as TextBox)?.SelectAll();
        }
    }
}
