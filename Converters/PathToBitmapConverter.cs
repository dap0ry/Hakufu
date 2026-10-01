using System.Globalization;
using Avalonia.Data.Converters;
using Hakufu.Services;

namespace Hakufu.Converters;

/// <summary>
/// Ruta local (string) → Bitmap. En WPF un Image aceptaba la ruta tal cual;
/// en Avalonia hace falta este converter: Source="{Binding X.Path, Converter={StaticResource PathToBitmap}}".
/// </summary>
public class PathToBitmapConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BitmapHelper.TryLoad(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
