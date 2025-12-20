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

namespace G33kJavu.Core.Scanning;

internal static class DefaultExclusions
{
    private static readonly string[] ExcludedDirectoryNames =
    [
        "bin",
        "obj",
        ".git",
        ".vs",
        ".idea",
        ".vscode",
        "node_modules"
    ];

    public static bool IsExcludedDirectory(DirectoryInfo dir)
    {
        var name = dir.Name;
        for (var i = 0; i < ExcludedDirectoryNames.Length; i++)
        {
            if (name.Equals(ExcludedDirectoryNames[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (name.StartsWith('.'))
            return true;

        if (dir.Attributes.HasFlag(FileAttributes.Hidden))
            return true;

        if (dir.Attributes.HasFlag(FileAttributes.ReparsePoint))
            return true;

        return false;
    }

    public static bool IsExcludedFileName(string fileName)
    {
        if (fileName.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
            return true;
        if (fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
            return true;
        if (fileName.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase))
            return true;
        if (fileName.EndsWith(".AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}

