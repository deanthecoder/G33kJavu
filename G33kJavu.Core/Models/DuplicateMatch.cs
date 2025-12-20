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

public sealed class DuplicateMatch
{
    public int FileAId { get; init; }
    public int FileBId { get; init; }

    public int AStartNormLine { get; init; }
    public int AEndNormLine { get; init; }
    public int BStartNormLine { get; init; }
    public int BEndNormLine { get; init; }

    public int MatchedLineCount { get; init; }

    public int AStartOriginalLineNumber { get; init; }
    public int AEndOriginalLineNumber { get; init; }
    public int BStartOriginalLineNumber { get; init; }
    public int BEndOriginalLineNumber { get; init; }

    public ulong HashId { get; init; }
}

