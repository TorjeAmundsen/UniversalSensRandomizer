using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace UniversalSensRandomizer.ViewModels;

public sealed class DisableToggleTextConverter : IValueConverter
{
    public static DisableToggleTextConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool disabled && disabled)
        {
            return "Disabled (1.00x baseline)";
        }
        return "Disable randomizer";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class StartStopTextConverter : IValueConverter
{
    public static StartStopTextConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool running && running)
        {
            return "Stop randomizer";
        }
        return "Start randomizer";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
