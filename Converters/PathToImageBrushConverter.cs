using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Hakufu.Converters;

// Convierte una ruta local (o null) en un ImageBrush para usar como fondo
// personalizado del panel de Inicio o de un botón de navegación. Si no hay
// ruta (o el archivo ya no existe), cae al brush por defecto — el
// ConverterParameter indica la clave del recurso a usar como fallback
// (p. ej. "CardBackground" o "SidebarBackground").
public class PathToImageBrushConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            try
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource     = new Uri(path, UriKind.Absolute);
                img.CacheOption   = BitmapCacheOption.OnLoad;
                img.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                img.EndInit();
                img.Freeze();
                return new ImageBrush(img) { Stretch = Stretch.UniformToFill };
            }
            catch { /* cae al fondo por defecto */ }
        }

        var fallbackKey = parameter as string ?? "CardBackground";
        return Application.Current.TryFindResource(fallbackKey);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
