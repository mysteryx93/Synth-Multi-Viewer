using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using HanumanInstitute.MediaPlayer.Avalonia;

namespace HanumanInstitute.SynthMultiViewer.Helpers;

/// <summary>
/// Connects view events to commands without depending on view model types.
/// </summary>
public static class EventCommands
{
    /// <summary>
    /// Defines the command executed when a control loads.
    /// </summary>
    public static readonly AttachedProperty<ICommand?> LoadedProperty = AvaloniaProperty.RegisterAttached<Control, ICommand?>("Loaded", typeof(EventCommands));

    /// <summary>
    /// Defines the command executed when a control loses focus.
    /// </summary>
    public static readonly AttachedProperty<ICommand?> LostFocusProperty = AvaloniaProperty.RegisterAttached<Control, ICommand?>("LostFocus", typeof(EventCommands));

    /// <summary>
    /// Defines the command executed on a single left-button press.
    /// </summary>
    public static readonly AttachedProperty<ICommand?> SingleClickProperty = AvaloniaProperty.RegisterAttached<Control, ICommand?>("SingleClick", typeof(EventCommands));

    /// <summary>
    /// Defines the command executed when a player loads media.
    /// </summary>
    public static readonly AttachedProperty<ICommand?> MediaLoadedProperty = AvaloniaProperty.RegisterAttached<Control, ICommand?>("MediaLoaded", typeof(EventCommands));

    /// <summary>
    /// Defines the parameter passed to event commands.
    /// </summary>
    public static readonly AttachedProperty<object?> ParameterProperty = AvaloniaProperty.RegisterAttached<Control, object?>("Parameter", typeof(EventCommands));

    static EventCommands()
    {
        Connect(LoadedProperty, Control.LoadedEvent);
        Connect(LostFocusProperty, InputElement.LostFocusEvent);
        Connect(SingleClickProperty, InputElement.PointerPressedEvent, e => e.ClickCount == 1 && e.GetCurrentPoint(null).Properties.IsLeftButtonPressed);
        Connect(MediaLoadedProperty, PlayerHostBase.MediaLoadedEvent);
    }

    /// <summary>
    /// Gets the command executed when the control loads.
    /// </summary>
    public static ICommand? GetLoaded(Control control) => control.GetValue(LoadedProperty);
    /// <summary>
    /// Sets the command executed when the control loads.
    /// </summary>
    public static void SetLoaded(Control control, ICommand? value) => control.SetValue(LoadedProperty, value);
    /// <summary>
    /// Gets the command executed when the control loses focus.
    /// </summary>
    public static ICommand? GetLostFocus(Control control) => control.GetValue(LostFocusProperty);
    /// <summary>
    /// Sets the command executed when the control loses focus.
    /// </summary>
    public static void SetLostFocus(Control control, ICommand? value) => control.SetValue(LostFocusProperty, value);
    /// <summary>
    /// Gets the command executed on a single left-button press.
    /// </summary>
    public static ICommand? GetSingleClick(Control control) => control.GetValue(SingleClickProperty);
    /// <summary>
    /// Sets the command executed on a single left-button press.
    /// </summary>
    public static void SetSingleClick(Control control, ICommand? value) => control.SetValue(SingleClickProperty, value);
    /// <summary>
    /// Gets the command executed when media loads.
    /// </summary>
    public static ICommand? GetMediaLoaded(Control control) => control.GetValue(MediaLoadedProperty);
    /// <summary>
    /// Sets the command executed when media loads.
    /// </summary>
    public static void SetMediaLoaded(Control control, ICommand? value) => control.SetValue(MediaLoadedProperty, value);
    /// <summary>
    /// Gets the parameter passed to event commands.
    /// </summary>
    public static object? GetParameter(Control control) => control.GetValue(ParameterProperty);
    /// <summary>
    /// Sets the parameter passed to event commands.
    /// </summary>
    public static void SetParameter(Control control, object? value) => control.SetValue(ParameterProperty, value);

    private static void Connect<TEventArgs>(AttachedProperty<ICommand?> property, RoutedEvent<TEventArgs> routedEvent, Func<TEventArgs, bool>? filter = null)
        where TEventArgs : RoutedEventArgs
    {
        EventHandler<TEventArgs> handler = (sender, e) =>
        {
            if (sender is Control control && (filter == null || filter(e)))
            {
                var command = control.GetValue(property);
                var parameter = GetParameter(control);
                if (command?.CanExecute(parameter) == true)
                {
                    command.Execute(parameter);
                }
            }
        };
        property.Changed.AddClassHandler<Control>((control, change) =>
        {
            control.RemoveHandler(routedEvent, handler);
            if (change.NewValue is ICommand)
            {
                control.AddHandler(routedEvent, handler);
            }
        });
    }
}
