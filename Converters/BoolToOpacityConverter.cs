using System.Globalization;
using System.Windows.Data;

namespace Hakufu.Converters;

// Usado para resaltar el botón de orden activo (Nombre/Fecha/Personalizado)
// sin necesitar un estado "seleccionado" completo en el estilo del botón.
[ValueConversion(typeof(bool), typeof(double))]
public class BoolToOpacityConverter : IValueConverter
{
    public double WhenTrue  { get; set; } = 1.0;
    public double WhenFalse { get; set; } = 0.5;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is bool b && b) ? WhenTrue : WhenFalse;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
