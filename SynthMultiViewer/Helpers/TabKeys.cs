using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace HanumanInstitute.SynthMultiViewer.Helpers;

/// <summary>
/// Routes Alt+Left/Right tab moves from the window so the strip does not steal them as navigation.
/// </summary>
public static class TabKeys
{
    /// <summary>
    /// Defines the command executed for Alt+Left.
    /// </summary>
    public static readonly AttachedProperty<ICommand?> MoveLeftProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>("MoveLeft", typeof(TabKeys));

    /// <summary>
    /// Defines the command executed for Alt+Right.
    /// </summary>
    public static readonly AttachedProperty<ICommand?> MoveRightProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>("MoveRight", typeof(TabKeys));

    static TabKeys()
    {
        MoveLeftProperty.Changed.AddClassHandler<Control>(OnCommandChanged);
        MoveRightProperty.Changed.AddClassHandler<Control>(OnCommandChanged);
    }

    /// <summary>
    /// Gets the move-left command attached to the control.
    /// </summary>
    public static ICommand? GetMoveLeft(Control control) => control.GetValue(MoveLeftProperty);

    /// <summary>
    /// Sets the move-left command attached to the control.
    /// </summary>
    public static void SetMoveLeft(Control control, ICommand? value) => control.SetValue(MoveLeftProperty, value);

    /// <summary>
    /// Gets the move-right command attached to the control.
    /// </summary>
    public static ICommand? GetMoveRight(Control control) => control.GetValue(MoveRightProperty);

    /// <summary>
    /// Sets the move-right command attached to the control.
    /// </summary>
    public static void SetMoveRight(Control control, ICommand? value) => control.SetValue(MoveRightProperty, value);

    private static void OnCommandChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        control.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        if (GetMoveLeft(control) != null || GetMoveRight(control) != null)
        {
            control.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        }
    }

    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || sender is not Control control) { return; }
        if (e.KeyModifiers != KeyModifiers.Alt || e.Key is not (Key.Left or Key.Right)) { return; }
        if (IsEditingField(control)) { return; }

        var command = e.Key == Key.Left ? GetMoveLeft(control) : GetMoveRight(control);
        if (command?.CanExecute(null) != true) { return; }

        command.Execute(null);
        e.Handled = true;
    }

    private static bool IsEditingField(Control root)
    {
        var focused = TopLevel.GetTopLevel(root)?.FocusManager?.GetFocusedElement() as Visual;
        for (var visual = focused; visual != null; visual = visual.GetVisualParent())
        {
            if (visual is TextBox or ComboBox or ComboBoxItem)
            {
                return true;
            }
        }

        return false;
    }
}
