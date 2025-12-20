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
using G33kJavu.ViewModels;

namespace G33kJavu.Converters;

public sealed class DiffMarkerKindToBrushConverter : IValueConverter
{
    public IBrush? ExactBrush { get; set; }
    public IBrush? NormalizedBrush { get; set; }
    public IBrush? DifferentBrush { get; set; }
    public IBrush? ErrorBrush { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DiffLineViewModel.MarkerKind kind)
            return DifferentBrush ?? Brushes.Gray;

        return kind switch
        {
            DiffLineViewModel.MarkerKind.Exact => ExactBrush ?? Brushes.LightGreen,
            DiffLineViewModel.MarkerKind.Normalized => NormalizedBrush ?? Brushes.Khaki,
            DiffLineViewModel.MarkerKind.Different => DifferentBrush ?? Brushes.IndianRed,
            DiffLineViewModel.MarkerKind.Error => ErrorBrush ?? Brushes.OrangeRed,
            _ => DifferentBrush ?? Brushes.Gray
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

