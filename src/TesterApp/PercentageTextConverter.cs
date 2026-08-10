using System.Globalization;
using System.Windows.Data;

namespace DmC.Qa.Tester;

public sealed class PercentageTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value?.ToString() ?? string.Empty;
        var numeric = new string(text
            .SkipWhile(character => character != '%' && !char.IsDigit(character))
            .SkipWhile(character => character == '%')
            .TakeWhile(character => char.IsDigit(character) || character is ',' or '.')
            .ToArray());

        if (decimal.TryParse(numeric, NumberStyles.Number, TurkishUi.Culture, out var result))
        {
            return Math.Clamp((double)result, 0, 100);
        }
        if (decimal.TryParse(numeric.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out result))
        {
            return Math.Clamp((double)result, 0, 100);
        }
        return 0d;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
