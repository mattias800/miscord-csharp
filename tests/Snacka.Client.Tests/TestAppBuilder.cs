using Avalonia;
using Avalonia.Headless;
using Avalonia.ReactiveUI;

[assembly: AvaloniaTestApplication(typeof(Snacka.Client.Tests.TestAppBuilder))]

namespace Snacka.Client.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>()
            .UseReactiveUI()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
