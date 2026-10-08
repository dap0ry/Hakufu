using Avalonia;
using Velopack;

namespace Hakufu;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Lo primero de todo: al instalar o actualizar, Velopack arranca la app con
        // argumentos propios, hace su trabajo y sale aquí mismo.
        VelopackApp.Build().Run();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // También lo usa el diseñador de Avalonia (previsualizador del IDE).
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
