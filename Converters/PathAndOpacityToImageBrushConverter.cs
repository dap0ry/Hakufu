using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Hakufu.Services;

namespace Hakufu.Converters;

// MultiBinding: values[0] = ruta local (o null), values[1] = opacidad (double).
// La opacidad va horneada en el propio Brush — así solo se atenúa la imagen,
// no el resto del contenido del botón (icono, texto), que quedaría afectado
// si se pusiera Opacity en el elemento entero.
// ConverterParameter = clave del brush de reserva (por defecto CardBackground).
public class PathAndOpacityToImageBrushConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var path    = values.Count > 0 ? values[0] as string : null;
        var opacity = values.Count > 1 && values[1] is double d ? d : 1.0;

        if (BitmapHelper.TryLoad(path) is { } bmp)
            return new ImageBrush(bmp) { Stretch = Stretch.UniformToFill, Opacity = opacity };

        var fallbackKey = parameter as string ?? "CardBackground";
        var app = Application.Current;
        return app is not null && app.TryGetResource(fallbackKey, app.ActualThemeVariant, out var res)
            ? res
            : null;
    }
}
