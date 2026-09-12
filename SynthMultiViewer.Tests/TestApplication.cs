using Avalonia;
using Avalonia.Headless;
using ReactiveUI.Avalonia;
using ReactiveUI.Builder;

[assembly: AvaloniaTestApplication(typeof(HanumanInstitute.SynthMultiViewer.Tests.TestApplication))]

namespace HanumanInstitute.SynthMultiViewer.Tests;

public static class TestApplication
{
    public static AppBuilder BuildAvaloniaApp()
    {
        RxAppBuilder.CreateReactiveUIBuilder().WithAvalonia().WithCoreServices().BuildApp();
        return AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .UseSkia()
            .UseReactiveUI(_ => { });
    }
}
