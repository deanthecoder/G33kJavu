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

namespace G33kJavu.Core.Models;

public sealed class ScanSettings
{
    public int K { get; set; } = 12;
    public int W { get; set; } = 8;
    public int MinReportLines { get; set; } = 20;
    public int GapAllowance { get; set; } = 1;
    public int MaxOccurrencesPerFingerprint { get; set; } = 100;
    public bool IgnoreStringContent { get; set; } = true;
    public bool IgnoreComments { get; set; } = true;
}
