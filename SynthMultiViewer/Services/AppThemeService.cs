using Avalonia;
using Avalonia.Styling;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <inheritdoc />
public class AppThemeService : IAppTheme
{
    /// <inheritdoc />
    public string RequestedTheme
    {
        get => Application.Current?.RequestedThemeVariant?.Key.ToString() ?? string.Empty;
        set
        {
            if (Application.Current is not { } app) { return; }

            app.RequestedThemeVariant = string.Equals(value, nameof(ThemeVariant.Dark), StringComparison.OrdinalIgnoreCase)
                ? ThemeVariant.Dark
                : ThemeVariant.Light;
        }
    }
}
