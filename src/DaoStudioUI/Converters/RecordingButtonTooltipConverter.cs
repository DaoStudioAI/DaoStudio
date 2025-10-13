using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace DaoStudioUI.Converters
{
    public class RecordingButtonTooltipConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isRecording)
            {
                return isRecording ? "Stop Recording" : "Start Voice Recording";
            }
            
            return "Voice Recording";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}