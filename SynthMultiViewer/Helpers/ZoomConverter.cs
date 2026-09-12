using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace HanumanInstitute.SynthMultiViewer.Helpers;

/// <summary>
/// Converts a zoom value to string, where 1 = 100%.
/// </summary>
public sealed class ZoomConverter : IValueConverter
{
    /// <summary>
    /// Gets or sets the label used for scale-to-fit mode.
    /// </summary>
    public string ZeroText { get; set; } = "Scale to Fit";

    /// <summary>
    /// Formats a zoom factor as a percentage, or the scale-to-fit label for zero.
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double number || number == 0)
        {
            return ZeroText;
        }

        var decimals = parameter is int d ? d : 0;
        var percent = number * 100;
        return decimals > 0
            ? percent.ToString("0." + new string('0', decimals), culture) + "%"
            : Math.Round(percent).ToString("0", culture) + "%";
    }

    /// <summary>
    /// Parses a percentage or the scale-to-fit label into a zoom factor.
    /// Incomplete text leaves the bound zoom unchanged.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = (value as string)?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return BindingOperations.DoNothing;
        }

        if (string.Equals(text, ZeroText, StringComparison.OrdinalIgnoreCase))
        {
            return 0.0;
        }

        if (text.EndsWith('%'))
        {
            text = text[..^1].Trim();
        }

        return double.TryParse(text, NumberStyles.Float, culture, out var result)
            ? result / 100.0
            : BindingOperations.DoNothing;
    }
}
