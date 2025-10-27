using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace DaoStudioUI.Converters
{
    /// <summary>
    /// Converter that returns the accent color when step debugging is enabled,
    /// or returns null for the default button background when disabled.
    /// Properly handles both dark and light themes.
    /// </summary>
    public class StepDebuggingButtonBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isEnabled && isEnabled)
            {
                // Get the current theme variant from the application
                var currentTheme = Application.Current?.ActualThemeVariant;
                
                // Try to get the accent color brush from the theme resources
                if (Application.Current?.TryGetResource("AccentFillColorDefaultBrush", 
                    currentTheme, out var accentBrush) == true && accentBrush is IBrush brush)
                {
                    return brush;
                }
                
                // Fallback: Create a suitable highlight color based on the current theme
                // Check if we're in dark theme
                var isDarkTheme = currentTheme == ThemeVariant.Dark;
                
                // Return appropriate highlight color for dark or light theme
                // Dark theme: lighter semi-transparent overlay
                // Light theme: darker semi-transparent overlay
                return new SolidColorBrush(isDarkTheme 
                    ? Color.FromArgb(80, 255, 255, 255)  // Light overlay for dark theme
                    : Color.FromArgb(40, 0, 0, 0));      // Dark overlay for light theme
            }
            
            return null; // Return null to use the default button background when disabled
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
