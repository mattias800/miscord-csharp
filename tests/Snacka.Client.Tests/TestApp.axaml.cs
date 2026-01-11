using Avalonia;
using Avalonia.Markup.Xaml;

namespace Snacka.Client.Tests;

public class TestApp : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
