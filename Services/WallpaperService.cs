using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Hakufu.Services;

public class WallpaperService : IWallpaperService
{
    public void Apply(string? imagePath, double opacity)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            // Sin override: vuelve a caer al AppBackground normal del tema
            // (definido en LightTheme.xaml / DarkTheme.xaml).
            Application.Current.Resources.Remove("AppBackground");
            return;
        }

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource     = new Uri(imagePath, UriKind.Absolute);
            bmp.CacheOption   = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bmp.EndInit();
            bmp.Freeze();

            var brush = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill, Opacity = opacity };
            brush.Freeze();

            // Asignado directamente en Application.Resources (no en una de
            // sus MergedDictionaries) — una clave puesta aquí siempre gana a
            // la misma clave definida solo dentro de una merged dictionary,
            // así que sustituye al AppBackground del tema activo sin
            // pelearse con ThemeService.SetTheme() cuando cambia de tema.
            Application.Current.Resources["AppBackground"] = brush;
        }
        catch
        {
            Application.Current.Resources.Remove("AppBackground");
        }
    }
}
