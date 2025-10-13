using System;
using System.Globalization;
using DaoStudioUI.Models;
using Avalonia;
using Avalonia.Data.Converters;
using DaoStudio;
using DaoStudio.Interfaces;

namespace DaoStudioUI.Converters
{
    public class MessageToAlignmentConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ChatMessage message)
            {
                // Center time marker messages (system messages with empty content)
                if (message.Role == MessageRole.System && string.IsNullOrEmpty(message.Content))
                {
                    return Avalonia.Layout.HorizontalAlignment.Center;
                }
                    
                return message.Role switch
                {
                    MessageRole.User => Avalonia.Layout.HorizontalAlignment.Right,
                    MessageRole.Assistant => Avalonia.Layout.HorizontalAlignment.Left,
                    _ => Avalonia.Layout.HorizontalAlignment.Left
                };
            }
            
            return Avalonia.Layout.HorizontalAlignment.Left;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}