using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.SynthMultiViewer.Models;
using HanumanInstitute.SynthMultiViewer.Services;
using Splat;

namespace HanumanInstitute.SynthMultiViewer;

/// <summary>
/// Initializes application resources and the desktop workspace.
/// </summary>
public class App : Application
{
    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            var main = ViewModelLocator.Main;
            var settings = Locator.Current.GetService<ISettingsProvider<AppSettingsData>>();
            var theme = Locator.Current.GetService<IAppTheme>();
            Locator.Current.GetService<IFrameworkDetectionService>();
            if (settings != null && theme != null)
            {
                theme.RequestedTheme = settings.Value.Theme.ToString();
            }

            Locator.Current.GetService<IDialogService>()!.Show(null, main);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
