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
using G33kJavu.Core.Models;

namespace G33kJavu.ViewModels;

public sealed class DuplicateMatchViewModel
{
    public required DuplicateMatch Model { get; init; }

    public int MatchedLineCount => Model.MatchedLineCount;

    public required string FileAName { get; init; }
    public required string FileBName { get; init; }
    public required string FileAFullPath { get; init; }
    public required string FileBFullPath { get; init; }
    public required string RangeSummary { get; init; }
    public required string IdSummary { get; init; }

    public static DuplicateMatchViewModel Create(DuplicateMatch match, ProcessedFile fileA, ProcessedFile fileB)
    {
        var aName = fileA.Path.Name;
        var bName = fileB.Path.Name;
        var id = match.HashId.ToString("X16");
        return new DuplicateMatchViewModel
        {
            Model = match,
            FileAName = aName,
            FileBName = bName,
            FileAFullPath = fileA.Path.FullName,
            FileBFullPath = fileB.Path.FullName,
            RangeSummary = $"A {match.AStartOriginalLineNumber}-{match.AEndOriginalLineNumber}  ↔  B {match.BStartOriginalLineNumber}-{match.BEndOriginalLineNumber}",
            IdSummary = $"ID {id[..8]}"
        };
    }
}
