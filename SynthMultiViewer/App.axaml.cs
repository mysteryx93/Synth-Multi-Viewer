using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HanumanInstitute.SynthMultiViewer.Models;
using HanumanInstitute.SynthMultiViewer.Services;
using HanumanInstitute.SynthMultiViewer.Views;
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
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var main = ViewModelLocator.Main;
            var settings = Locator.Current.GetService<ISettingsProvider<AppSettingsData>>();
            var theme = Locator.Current.GetService<IAppTheme>();
            Locator.Current.GetService<IFrameworkDetectionService>();
            if (settings != null && theme != null)
            {
                theme.RequestedTheme = settings.Value.Theme.ToString();
            }

            desktop.MainWindow = new MainView
            {
                DataContext = main
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
