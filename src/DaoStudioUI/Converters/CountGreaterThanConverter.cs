using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace DaoStudioUI.Converters
{
    public class CountGreaterThanConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int count && parameter is string paramStr && int.TryParse(paramStr, out int compareValue))
            {
                return count > compareValue;
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
