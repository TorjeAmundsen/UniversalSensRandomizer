using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace UniversalSensRandomizer.ViewModels;

// Converts double <-> decimal? for NumericUpDown bindings. When the user clears the
// field the NUD pushes null back to the source; without this converter Avalonia tries
// to coerce null into a non-nullable double and surfaces System.InvalidCastException
// in the input field. Returning BindingOperations.DoNothing leaves the source value
// unchanged in that case.
public sealed class NudDoubleConverter : IValueConverter
{
    public static NudDoubleConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is double d ? (decimal)d : (object?)null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is decimal d ? (double)d : BindingOperations.DoNothing;
    }
}

public sealed class NudIntConverter : IValueConverter
{
    public static NudIntConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int i ? (decimal)i : (object?)null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is decimal d ? (int)d : BindingOperations.DoNothing;
    }
}

