using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Hakufu.Converters;

// MultiBinding: values[0] = ruta local (o null), values[1] = opacidad (double).
// Igual que PathToImageBrushConverter pero con la opacidad horneada en el
// propio Brush — así solo se atenúa la imagen, no el resto del contenido del
// botón (icono, texto), que quedaría afectado si se pusiera Opacity en el
// elemento entero.
public class PathAndOpacityToImageBrushConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var path    = values.Length > 0 ? values[0] as string : null;
        var opacity = values.Length > 1 && values[1] is double d ? d : 1.0;

        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
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
                var brush = new ImageBrush(img) { Stretch = Stretch.UniformToFill, Opacity = opacity };
                brush.Freeze();
                return brush;
            }
            catch { /* cae al fondo por defecto */ }
        }

        var fallbackKey = parameter as string ?? "CardBackground";
        return Application.Current.TryFindResource(fallbackKey);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
