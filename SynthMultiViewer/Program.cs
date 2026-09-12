using Avalonia;
using ReactiveUI.Avalonia;
using ReactiveUI.Builder;

namespace HanumanInstitute.SynthMultiViewer;

internal static class Program
{
    /// <summary>
    /// Configures ReactiveUI and starts the desktop application.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        RxAppBuilder.CreateReactiveUIBuilder()
            .WithAvalonia()
            .WithCoreServices()
            .BuildApp();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Configures Avalonia for desktop startup and the previewer.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseReactiveUI(_ => { });
}
