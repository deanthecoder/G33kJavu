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

namespace G33kJavu.ViewModels;

public sealed class DiffLineViewModel
{
    public enum MarkerKind
    {
        Exact,
        Normalized,
        Different,
        Error
    }

    public int? ALineNumber { get; init; }
    public int? BLineNumber { get; init; }

    public string? AText { get; init; }
    public string? BText { get; init; }
    public string? AToolTip { get; init; }
    public string? BToolTip { get; init; }

    public MarkerKind Kind { get; init; }
    public string MarkerText { get; init; } = string.Empty;
    public string MarkerToolTip { get; init; } = string.Empty;

    public bool IsInMatchedBlock { get; init; }
    public bool IsEllipsis { get; init; }
}
