using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Naming.AdvConfig.Services
{
    /// <summary>
    /// Static converters for boolean values to various string representations.
    /// </summary>
    public static class BoolConverters
    {
        /// <summary>
        /// Converts boolean to "Enabled"/"Disabled" string representation.
        /// </summary>
        public static readonly IValueConverter EnabledDisabled = new FuncValueConverter<bool, string>(
            value => value ? "Enabled" : "Disabled");

        /// <summary>
        /// Converts boolean to "Complete"/"Incomplete" string representation.
        /// </summary>
        public static readonly IValueConverter CompleteIncomplete = new FuncValueConverter<bool, string>(
            value => value ? "Complete" : "Incomplete");

        /// <summary>
        /// Converts boolean to success/error brush color.
        /// </summary>
        public static readonly IValueConverter SuccessError = new FuncValueConverter<bool, object>(
            value => value ? "SystemFillColorSuccessBrush" : "SystemFillColorCriticalBrush");
    }
}
