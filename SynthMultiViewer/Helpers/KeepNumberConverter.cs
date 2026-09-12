using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;

namespace HanumanInstitute.SynthMultiViewer.Helpers;

/// <summary>
/// Leaves the bound number unchanged when NumericUpDown is cleared.
/// </summary>
public sealed class KeepNumberConverter : IValueConverter
{
    /// <summary>
    /// Gets the shared converter instance.
    /// </summary>
    public static KeepNumberConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? BindingOperations.DoNothing : value;
}

/// <summary>
/// Restores a NumericUpDown to its last value when it is left empty.
/// </summary>
public static class NumericKeep
{
    /// <summary>
    /// Defines whether an empty value reverts on lost focus.
    /// </summary>
    public static readonly AttachedProperty<bool> RevertEmptyProperty =
        AvaloniaProperty.RegisterAttached<NumericUpDown, bool>("RevertEmpty", typeof(NumericKeep));

    private static readonly AttachedProperty<decimal?> LastValueProperty =
        AvaloniaProperty.RegisterAttached<NumericUpDown, decimal?>("LastValue", typeof(NumericKeep));

    static NumericKeep()
    {
        RevertEmptyProperty.Changed.AddClassHandler<NumericUpDown>((box, change) =>
        {
            box.LostFocus -= OnLostFocus;
            if (change.NewValue is true)
            {
                box.LostFocus += OnLostFocus;
                if (box.Value.HasValue)
                {
                    box.SetValue(LastValueProperty, box.Value);
                }
            }
        });
        NumericUpDown.ValueProperty.Changed.AddClassHandler<NumericUpDown>((box, change) =>
        {
            if (GetRevertEmpty(box) && change.NewValue is decimal value)
            {
                box.SetValue(LastValueProperty, value);
            }
        });
    }

    /// <summary>
    /// Gets whether empty input reverts to the last value.
    /// </summary>
    public static bool GetRevertEmpty(NumericUpDown box) => box.GetValue(RevertEmptyProperty);

    /// <summary>
    /// Sets whether empty input reverts to the last value.
    /// </summary>
    public static void SetRevertEmpty(NumericUpDown box, bool value) => box.SetValue(RevertEmptyProperty, value);

    private static void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is NumericUpDown box && GetRevertEmpty(box) && !box.Value.HasValue)
        {
            var last = box.GetValue(LastValueProperty);
            if (last.HasValue)
            {
                box.Value = last;
            }
        }
    }
}
