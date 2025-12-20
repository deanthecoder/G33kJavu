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

using System.IO;
using G33kJavu.Core.Models;

namespace G33kJavu.ViewModels;

public sealed class FileItemViewModel
{
    public int FileId { get; init; }
    public required FileInfo File { get; init; }
    public FileCategory Category { get; init; }
    public required string RelativePath { get; init; }
}
