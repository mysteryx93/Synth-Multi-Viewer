using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaPlayer = HanumanInstitute.MediaPlayer.Avalonia.MediaPlayer;

namespace HanumanInstitute.SynthMultiViewer.Helpers;

/// <summary>
/// Routes viewer playback keys from the window so they work without player focus.
/// </summary>
public static class ViewerKeys
{
    /// <summary>
    /// Defines the command executed for Left, Right, Ctrl+Left, and Ctrl+Right.
    /// </summary>
    public static readonly AttachedProperty<ICommand?> SeekProperty = AvaloniaProperty.RegisterAttached<Control, ICommand?>("Seek", typeof(ViewerKeys));

    /// <summary>
    /// Defines the command executed for Space.
    /// </summary>
    public static readonly AttachedProperty<ICommand?> PlayPauseProperty = AvaloniaProperty.RegisterAttached<Control, ICommand?>("PlayPause", typeof(ViewerKeys));

    /// <summary>
    /// Defines the command executed for Ctrl+C in the viewer.
    /// </summary>
    public static readonly AttachedProperty<ICommand?> CopyFrameProperty = AvaloniaProperty.RegisterAttached<Control, ICommand?>("CopyFrame", typeof(ViewerKeys));

    /// <summary>
    /// Defines the command executed for +, =, and numpad Add.
    /// </summary>
    public static readonly AttachedProperty<ICommand?> ZoomInProperty = AvaloniaProperty.RegisterAttached<Control, ICommand?>("ZoomIn", typeof(ViewerKeys));

    /// <summary>
    /// Defines the command executed for -, _, and numpad Subtract.
    /// </summary>
    public static readonly AttachedProperty<ICommand?> ZoomOutProperty = AvaloniaProperty.RegisterAttached<Control, ICommand?>("ZoomOut", typeof(ViewerKeys));

    static ViewerKeys()
    {
        SeekProperty.Changed.AddClassHandler<Control>(OnCommandChanged);
        PlayPauseProperty.Changed.AddClassHandler<Control>(OnCommandChanged);
        CopyFrameProperty.Changed.AddClassHandler<Control>(OnCommandChanged);
        ZoomInProperty.Changed.AddClassHandler<Control>(OnCommandChanged);
        ZoomOutProperty.Changed.AddClassHandler<Control>(OnCommandChanged);
    }

    /// <summary>
    /// Gets the seek command attached to the control.
    /// </summary>
    public static ICommand? GetSeek(Control control) => control.GetValue(SeekProperty);

    /// <summary>
    /// Sets the seek command attached to the control.
    /// </summary>
    public static void SetSeek(Control control, ICommand? value) => control.SetValue(SeekProperty, value);

    /// <summary>
    /// Gets the play/pause command attached to the control.
    /// </summary>
    public static ICommand? GetPlayPause(Control control) => control.GetValue(PlayPauseProperty);

    /// <summary>
    /// Sets the play/pause command attached to the control.
    /// </summary>
    public static void SetPlayPause(Control control, ICommand? value) => control.SetValue(PlayPauseProperty, value);

    /// <summary>
    /// Gets the copy-frame command attached to the control.
    /// </summary>
    public static ICommand? GetCopyFrame(Control control) => control.GetValue(CopyFrameProperty);

    /// <summary>
    /// Sets the copy-frame command attached to the control.
    /// </summary>
    public static void SetCopyFrame(Control control, ICommand? value) => control.SetValue(CopyFrameProperty, value);

    /// <summary>
    /// Gets the zoom-in command attached to the control.
    /// </summary>
    public static ICommand? GetZoomIn(Control control) => control.GetValue(ZoomInProperty);

    /// <summary>
    /// Sets the zoom-in command attached to the control.
    /// </summary>
    public static void SetZoomIn(Control control, ICommand? value) => control.SetValue(ZoomInProperty, value);

    /// <summary>
    /// Gets the zoom-out command attached to the control.
    /// </summary>
    public static ICommand? GetZoomOut(Control control) => control.GetValue(ZoomOutProperty);

    /// <summary>
    /// Sets the zoom-out command attached to the control.
    /// </summary>
    public static void SetZoomOut(Control control, ICommand? value) => control.SetValue(ZoomOutProperty, value);

    private static void OnCommandChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        control.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        if (GetSeek(control) != null || GetPlayPause(control) != null || GetCopyFrame(control) != null ||
            GetZoomIn(control) != null || GetZoomOut(control) != null)
        {
            control.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        }
    }

    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || sender is not Control control) { return; }
        if (IsEditingText(control)) { return; }

        if (TrySeek(control, e) || TryPlayPause(control, e) || TryCopyFrame(control, e) || TryZoom(control, e) ||
            TryToggleFullScreen(control, e))
        {
            e.Handled = true;
        }
    }

    private static bool TrySeek(Control control, KeyEventArgs e)
    {
        if (e.Key is not (Key.Left or Key.Right)) { return false; }
        if ((e.KeyModifiers & ~KeyModifiers.Control) != KeyModifiers.None) { return false; }

        var frames = e.Key == Key.Right ? 1 : -1;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            frames *= 10;
        }

        return Execute(GetSeek(control), frames);
    }

    private static bool TryPlayPause(Control control, KeyEventArgs e)
    {
        if (e.Key != Key.Space || e.KeyModifiers != KeyModifiers.None) { return false; }

        return Execute(GetPlayPause(control), null);
    }

    private static bool TryCopyFrame(Control control, KeyEventArgs e)
    {
        if (e.Key != Key.C || e.KeyModifiers != KeyModifiers.Control) { return false; }

        return Execute(GetCopyFrame(control), control);
    }

    private static bool TryZoom(Control control, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None) { return false; }
        if (e.Key is Key.OemPlus or Key.Add)
        {
            return Execute(GetZoomIn(control), null);
        }

        if (e.Key is Key.OemMinus or Key.Subtract)
        {
            return Execute(GetZoomOut(control), null);
        }

        return false;
    }

    private static bool TryToggleFullScreen(Control control, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers != KeyModifiers.Alt) { return false; }

        var player = control.GetVisualDescendants().OfType<AvaloniaPlayer>()
            .FirstOrDefault(x => x.IsEffectivelyVisible);
        return Execute(player?.ToggleFullScreenCommand, null);
    }

    private static bool Execute(ICommand? command, object? parameter)
    {
        if (command?.CanExecute(parameter) != true) { return false; }

        command.Execute(parameter);
        return true;
    }

    private static bool IsEditingText(Control root)
    {
        var focused = TopLevel.GetTopLevel(root)?.FocusManager.GetFocusedElement() as Visual;
        for (var visual = focused; visual != null; visual = visual.GetVisualParent())
        {
            if (visual is TextBox or ComboBox or ComboBoxItem or TextEditor or CompletionWindow
                or OverloadInsightWindow)
            {
                return true;
            }
        }

        return false;
    }
}
