using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace HanumanInstitute.SynthMultiViewer.Helpers;

/// <summary>
/// Passes dropped local file paths to a command.
/// </summary>
public static class FileDropBehavior
{
    /// <summary>
    /// Defines the command receiving an enumerable of local file paths.
    /// </summary>
    public static readonly AttachedProperty<ICommand?> CommandProperty = AvaloniaProperty.RegisterAttached<Control, ICommand?>("Command", typeof(FileDropBehavior));

    static FileDropBehavior()
    {
        DragDrop.DragEnterEvent.AddClassHandler<Control>(OnDragOver, RoutingStrategies.Bubble);
        DragDrop.DragOverEvent.AddClassHandler<Control>(OnDragOver, RoutingStrategies.Bubble);
        DragDrop.DropEvent.AddClassHandler<Control>(OnDrop, RoutingStrategies.Bubble);
    }

    /// <summary>
    /// Gets the command receiving dropped file paths.
    /// </summary>
    public static ICommand? GetCommand(Control control) => control.GetValue(CommandProperty);
    /// <summary>
    /// Sets the command receiving dropped file paths. Enable DragDrop.AllowDrop on the control.
    /// </summary>
    public static void SetCommand(Control control, ICommand? value) => control.SetValue(CommandProperty, value);

    private static string[] GetFiles(DragEventArgs e) =>
        e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>()
            .Select(file => file.TryGetLocalPath()).OfType<string>().ToArray() ?? [];

    private static ICommand? FindCommand(object? source)
    {
        for (var visual = source as Visual; visual != null; visual = visual.GetVisualParent())
        {
            if (visual is Control control)
            {
                var command = GetCommand(control);
                if (command != null)
                {
                    return command;
                }
            }
        }

        return null;
    }

    private static void OnDragOver(Control _, DragEventArgs e)
    {
        if (e.Handled) { return; }

        var files = GetFiles(e);
        if (files.Length == 0) { return; }

        var command = FindCommand(e.Source);
        if (command?.CanExecute(files) != true) { return; }

        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private static void OnDrop(Control _, DragEventArgs e)
    {
        if (e.Handled) { return; }

        var files = GetFiles(e);
        var command = FindCommand(e.Source);
        if (files.Length == 0 || command?.CanExecute(files) != true) { return; }

        command.Execute(files);
        e.Handled = true;
    }
}
