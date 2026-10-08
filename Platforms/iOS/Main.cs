using UIKit;

namespace Hakufu.Platforms.iOS;

/// <summary>Arranque en iPhone/iPad (el de escritorio es Program.cs, que aquí no se compila).</summary>
public static class IosProgram
{
    public static void Main(string[] args)
    {
        // Antes que nada: rutas y comportamiento de iOS, para que App ya los vea.
        IosPlatform.Configure();
        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
