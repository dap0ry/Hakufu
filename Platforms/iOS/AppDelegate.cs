using Avalonia;
using Avalonia.iOS;
using Foundation;

namespace Hakufu.Platforms.iOS;

/// <summary>
/// Avalonia en iOS: la App de siempre con MainView como vista única
/// (ISingleViewApplicationLifetime, ver App.StartSingleView).
/// </summary>
[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        => base.CustomizeAppBuilder(builder).WithInterFont().LogToTrace();
}
