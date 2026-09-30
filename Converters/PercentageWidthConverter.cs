using System.Globalization;
using Avalonia.Data.Converters;

namespace Hakufu.Converters;

/// <summary>MultiBinding converter: returns (percentage / 100) * totalWidth.</summary>
public class PercentageWidthConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return 0d;
        double pct   = values[0] is double d ? d : 0;
        double total = values[1] is double w ? w : 0;
        return Math.Max(0, pct / 100.0 * total);
    }
}
