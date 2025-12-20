// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any
// purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace G33kJavu.Converters;

public sealed class BoolToBrushConverter : IValueConverter
{
    public static BoolToBrushConverter Instance { get; } = new BoolToBrushConverter();

    public IBrush? TrueBrush { get; set; }
    public IBrush? FalseBrush { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return TrueBrush ?? Brushes.Transparent;
        return FalseBrush ?? Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

