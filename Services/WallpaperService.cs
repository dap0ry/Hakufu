using Avalonia;
using Avalonia.Media;

namespace Hakufu.Services;

public class WallpaperService : IWallpaperService
{
    public void Apply(string? imagePath, double opacity)
    {
        var resources = Application.Current!.Resources;

        var bmp = BitmapHelper.TryLoad(imagePath);
        if (bmp is null)
        {
            // Sin override: vuelve a caer al AppBackground normal del tema
            // (definido en LightTheme.axaml / DarkTheme.axaml).
            resources.Remove("AppBackground");
            return;
        }

        // Asignado directamente en Application.Resources (no en una de sus
        // MergedDictionaries): una clave puesta aquí gana a la misma clave del
        // diccionario del tema, así que sustituye al AppBackground activo sin
        // pelearse con ThemeService.SetTheme() cuando cambia de tema.
        resources["AppBackground"] = new ImageBrush(bmp)
        {
            Stretch = Stretch.UniformToFill,
            Opacity = opacity
        };
    }
}
