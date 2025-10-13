using System;
using System.Globalization;
using DaoStudioUI.Models;
using Avalonia.Data.Converters;
using DaoStudio;
using DaoStudio.Interfaces;

namespace DaoStudioUI.Converters
{
    public class MessageTypeToVisibilityConverter : IValueConverter
    {        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is MessageType messageType && parameter is string typeString)
            {
                if (Enum.TryParse<MessageType>(typeString, out MessageType requestedType))
                {
                    return messageType.HasFlag(requestedType);
                }
            }
            
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}