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

using System.Collections.ObjectModel;
using DTC.Core.ViewModels;

namespace G33kJavu.ViewModels;

public sealed class FolderNodeViewModel : ViewModelBase
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }

    public ObservableCollection<FolderNodeViewModel> Children { get; } = [];
}
