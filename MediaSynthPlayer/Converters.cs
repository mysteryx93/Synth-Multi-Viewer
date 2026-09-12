using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace HanumanInstitute.MediaSynthUI;

/// <summary>
/// Converts square-pixel mode to a bitmap interpolation mode.
/// </summary>
public sealed class SquarePixelsConverter : IValueConverter
{
    /// <summary>
    /// Gets the shared converter instance.
    /// </summary>
    public static readonly SquarePixelsConverter Instance = new();

    /// <summary>
    /// Selects nearest-neighbor interpolation for square pixels, or high-quality interpolation otherwise.
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        value is true ? BitmapInterpolationMode.None : BitmapInterpolationMode.HighQuality;

    /// <summary>
    /// Reverse conversion is not supported.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
