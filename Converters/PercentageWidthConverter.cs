using System.Globalization;
using System.Windows.Data;

namespace Hakufu.Converters;

/// <summary>MultiBinding converter: returns (percentage / 100) * totalWidth.</summary>
public class PercentageWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return 0d;
        double pct   = values[0] is double d ? d : 0;
        double total = values[1] is double w ? w : 0;
        return Math.Max(0, pct / 100.0 * total);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
