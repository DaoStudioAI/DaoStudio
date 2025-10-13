using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DaoStudioUI.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isActive && isActive)
            {
                return new SolidColorBrush(Colors.Red);
            }
            
            return null; // Return null to use the default button background
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}