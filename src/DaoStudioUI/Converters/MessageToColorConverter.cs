using System;
using System.Globalization;
using DaoStudioUI.Models;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using DaoStudio;
using DaoStudio.Interfaces;

namespace DaoStudioUI.Converters
{
    public class MessageToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ChatMessage message)
            {
                // Check for time marker messages (system messages with empty content)
                if (message.Role == MessageRole.System && string.IsNullOrEmpty(message.Content))
                {
                    return Brushes.Transparent; // Time markers should be transparent
                }
                    
                // Use theme resources instead of hard-coded colors
                var resourceKey = message.Role switch
                {
                    MessageRole.User => "UserMessageColorBrush",
                    MessageRole.Assistant => "AssistantMessageColorBrush",
                    MessageRole.System => "SystemMessageColorBrush", 
                    _ => "DefaultMessageColorBrush"
                };
                
                // Try to get the resource from application resources with correct ThemeVariant parameter
                if (Avalonia.Application.Current?.Resources.TryGetResource(resourceKey, Avalonia.Application.Current.ActualThemeVariant, out var brush) == true)
                {
                    return brush;
                }
                
                // Fallback colors if resources aren't defined (with opacity for lighter appearance)
                return message.Role switch
                {
                    MessageRole.User => new SolidColorBrush(new Color(51, Colors.RoyalBlue.R, Colors.RoyalBlue.G, Colors.RoyalBlue.B)),
                    MessageRole.Assistant => new SolidColorBrush(new Color(25, Colors.Green.R, Colors.Green.G, Colors.Green.B)),
                    MessageRole.System => new SolidColorBrush(new Color(51, Colors.Orange.R, Colors.Orange.G, Colors.Orange.B)),
                     _ => new SolidColorBrush(new Color(51, Colors.Gray.R, Colors.Gray.G, Colors.Gray.B))
                };
            }
            
            return new SolidColorBrush(new Color(51, Colors.Gray.R, Colors.Gray.G, Colors.Gray.B));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}