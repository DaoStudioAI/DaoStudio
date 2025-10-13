using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Data;
using Avalonia;
using System;
using System.Collections.Generic;
using System.Globalization;
using Naming.ParallelExecution;
using Naming;
using Naming.AdvConfig.ViewModels;

namespace Naming.AdvConfig.Tabs
{
    public class ExecutionTypeToIconConverter : IValueConverter
    {
        public static ExecutionTypeToIconConverter Instance { get; } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ParallelExecutionType execType)
            {
                return execType switch
                {
                    ParallelExecutionType.None => "M12,2C6.48,2 2,6.48 2,12C2,17.52 6.48,22 12,22C17.52,22 22,17.52 22,12C22,6.48 17.52,2 12,2M12,20C7.59,20 4,16.41 4,12C4,7.59 7.59,4 12,4C16.41,4 20,7.59 20,12C20,16.41 16.41,20 12,20M15.5,11L10,8.5V15.5L15.5,13V11Z",
                    ParallelExecutionType.ParameterBased => "M7,2V4H8V18A4,4 0 0,0 12,22A4,4 0 0,0 16,18V4H17V2H7M11,16C10.4,16 10,15.6 10,15C10,14.4 10.4,14 11,14C11.6,14 12,14.4 12,15C12,15.6 11.6,16 11,16M13,12C12.4,12 12,11.6 12,11C12,10.4 12.4,10 13,10C13.6,10 14,10.4 14,11C14,11.6 13.6,12 13,12M11,8C10.4,8 10,7.6 10,7C10,6.4 10.4,6 11,6C11.6,6 12,6.4 12,7C12,7.6 11.6,8 11,8Z",
                    ParallelExecutionType.ListBased => "M7,5H21V7H7V5M7,13V11H21V13H7M4,4.5A1.5,1.5 0 0,1 5.5,6A1.5,1.5 0 0,1 4,7.5A1.5,1.5 0 0,1 2.5,6A1.5,1.5 0 0,1 4,4.5M4,10.5A1.5,1.5 0 0,1 5.5,12A1.5,1.5 0 0,1 4,13.5A1.5,1.5 0 0,1 2.5,12A1.5,1.5 0 0,1 4,10.5M7,19V17H21V19H7M4,16.5A1.5,1.5 0 0,1 5.5,18A1.5,1.5 0 0,1 4,19.5A1.5,1.5 0 0,1 2.5,18A1.5,1.5 0 0,1 4,16.5Z",
                    ParallelExecutionType.ExternalList => "M19,13H13V19H11V13H5V11H11V5H13V11H19V13Z",
                    _ => "M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2Z"
                };
            }
            return "";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ExecutionTypeToDisplayNameConverter : IValueConverter
    {
        public static ExecutionTypeToDisplayNameConverter Instance { get; } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ParallelExecutionType execType)
            {
                return execType switch
                {
                    ParallelExecutionType.None => "Disabled",
                    ParallelExecutionType.ParameterBased => "Parameter-Based",
                    ParallelExecutionType.ListBased => "List-Based",
                    ParallelExecutionType.ExternalList => "External List",
                    _ => execType.ToString()
                };
            }
            return "";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ExecutionTypeToDescriptionConverter : IValueConverter
    {
        public static ExecutionTypeToDescriptionConverter Instance { get; } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ParallelExecutionType execType)
            {
                return execType switch
                {
                    ParallelExecutionType.None => "Parallel execution is disabled. Tasks will be executed sequentially.",
                    ParallelExecutionType.ParameterBased => "Uses POCO parameters from request data as parallel source. Each parameter becomes a separate session.",
                    ParallelExecutionType.ListBased => "Uses a specific array/list parameter as parallel source. Each list item becomes a separate session.",
                    ParallelExecutionType.ExternalList => "Uses an external string list provided by configuration. Each string becomes a separate session parameter.",
                    _ => "Unknown execution type"
                };
            }
            return "";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ResultStrategyToDisplayNameConverter : IValueConverter
    {
        public static ResultStrategyToDisplayNameConverter Instance { get; } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ParallelResultStrategy strategy)
            {
                return strategy switch
                {
                    ParallelResultStrategy.StreamIndividual => "Stream Results",
                    ParallelResultStrategy.WaitForAll => "Wait for All",
                    ParallelResultStrategy.FirstResultWins => "First Result Wins",
                    _ => strategy.ToString()
                };
            }
            return "";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ResultStrategyToDescriptionConverter : IValueConverter
    {
        public static ResultStrategyToDescriptionConverter Instance { get; } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ParallelResultStrategy strategy)
            {
                return strategy switch
                {
                    ParallelResultStrategy.StreamIndividual => "Stream individual results as they complete",
                    ParallelResultStrategy.WaitForAll => "Wait for all sessions to complete before returning",
                    ParallelResultStrategy.FirstResultWins => "Return first result and cancel remaining sessions",
                    _ => "Unknown strategy"
                };
            }
            return "";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToColorConverter : IValueConverter
    {
        public static BoolToColorConverter Instance { get; } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // Use theme-aware brushes instead of hardcoded colors
                if (boolValue)
                {
                    if (Application.Current?.Resources.TryGetResource("SystemFillColorSuccessBrush", null, out var successBrush) == true)
                        return successBrush as IBrush;
                    return new SolidColorBrush(Colors.Green);
                }
                else
                {
                    if (Application.Current?.Resources.TryGetResource("SystemFillColorCriticalBrush", null, out var criticalBrush) == true)
                        return criticalBrush as IBrush;
                    return new SolidColorBrush(Colors.Red);
                }
            }
            if (Application.Current?.Resources.TryGetResource("TextFillColorSecondaryBrush", null, out var secondaryBrush) == true)
                return secondaryBrush as IBrush;
            return new SolidColorBrush(Colors.Gray);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class EnumEqualsConverter : IValueConverter
    {
        public static EnumEqualsConverter Instance { get; } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value != null && parameter != null)
            {
                // Convert parameter string to enum value
                if (Enum.TryParse(value.GetType(), parameter.ToString(), out var parameterValue))
                {
                    return value.Equals(parameterValue);
                }
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is true && parameter != null)
            {
                // Convert parameter string to enum value
                if (Enum.TryParse(targetType, parameter.ToString(), out var parameterValue))
                {
                    return parameterValue;
                }
            }
            return BindingOperations.DoNothing;
        }
    }

    public class DanglingBehaviorToBooleanConverter : IValueConverter
    {
        public static DanglingBehaviorToBooleanConverter Instance { get; } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DanglingBehavior behavior && parameter is string paramString)
            {
                if (Enum.TryParse<DanglingBehavior>(paramString, out var targetBehavior))
                {
                    return behavior == targetBehavior;
                }
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ErrorReportingBehaviorToBooleanConverter : IValueConverter
    {
        public static ErrorReportingBehaviorToBooleanConverter Instance { get; } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ErrorReportingBehavior behavior && parameter is string paramString)
            {
                if (Enum.TryParse<ErrorReportingBehavior>(paramString, out var targetBehavior))
                {
                    return behavior == targetBehavior;
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
