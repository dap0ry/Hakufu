using System.Globalization;
using Avalonia.Data.Converters;

namespace Hakufu.Converters;

/// <summary>
/// Columnas de la rejilla de tomos: [ItemsPerRow, MainView.IsCompact]. En iPhone
/// (compacto) siempre 3; si no, las que se eligieron con el deslizador.
/// </summary>
public class CompactColumnsConverter : IMultiValueConverter
{
    public const int PhoneColumns = 3;

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var compact = values.Count > 1 && values[1] is true;
        if (compact) return PhoneColumns;
        return values.Count > 0 && values[0] is int n && n > 0 ? n : 1;
    }
}
