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
using System.IO;

namespace G33kJavu.Core.Models;

public sealed class ProcessedFile
{
    public int FileId { get; init; }
    public required FileInfo Path { get; init; }
    public FileCategory Category { get; init; }
    public ulong ContentHash { get; init; }

    /// <summary>
    /// Indexed by normalized line index, returns original 1-based line number in the source file.
    /// </summary>
    public int[] OriginalLineNumbers { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Indexed by normalized line index.
    /// </summary>
    public ulong[] LineHashes { get; init; } = Array.Empty<ulong>();

    public int NormalizedLineCount => LineHashes?.Length ?? 0;
}
