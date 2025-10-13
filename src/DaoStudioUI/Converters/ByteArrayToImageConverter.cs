using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using System;
using System.Globalization;
using System.IO;

namespace DaoStudioUI.Converters
{
    public class ByteArrayToImageConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is byte[] imageData && imageData.Length > 0)
            {
                try
                {
                    using (var ms = new MemoryStream(imageData))
                    {
                        return new Bitmap(ms);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but don't crash
                    Console.WriteLine($"Error converting image data: {ex.Message}");
                }
            }
            
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}