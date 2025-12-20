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
using System.Collections.Generic;
using System.IO;
using G33kJavu.Core.Models;

namespace G33kJavu.Core.Scanning;

internal static class FileEnumerator
{
    public sealed record FileToProcess(FileInfo File, FileCategory Category);

    private static readonly Dictionary<string, FileCategory> ExtensionToCategory = new Dictionary<string, FileCategory>(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = FileCategory.CSharp,
        [".c"] = FileCategory.Cpp,
        [".cpp"] = FileCategory.Cpp,
        [".h"] = FileCategory.Cpp,
        [".py"] = FileCategory.Python,
        [".js"] = FileCategory.JavaScript
    };

    public static List<FileToProcess> Enumerate(DirectoryInfo root)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));

        var results = new List<FileToProcess>(capacity: 4096);
        var seenDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var stack = new Stack<DirectoryInfo>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            if (DefaultExclusions.IsExcludedDirectory(dir))
                continue;

            var dirKey = NormalizePathKey(dir.FullName);
            if (!seenDirs.Add(dirKey))
                continue;

            FileInfo[] files;
            DirectoryInfo[] dirs;
            try
            {
                files = dir.GetFiles();
                dirs = dir.GetDirectories();
            }
            catch
            {
                continue;
            }

            for (var i = 0; i < dirs.Length; i++)
                stack.Push(dirs[i]);

            for (var i = 0; i < files.Length; i++)
            {
                var file = files[i];
                if (DefaultExclusions.IsExcludedFileName(file.Name))
                    continue;

                if (!ExtensionToCategory.TryGetValue(file.Extension, out var category))
                    continue;

                var fileKey = NormalizePathKey(file.FullName);
                if (!seenFiles.Add(fileKey))
                    continue;

                results.Add(new FileToProcess(file, category));
            }
        }

        results.Sort(static (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.File.FullName, b.File.FullName));
        return results;
    }

    private static string NormalizePathKey(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        try
        {
            path = Path.GetFullPath(path);
        }
        catch
        {
            // Ignore.
        }

        return path
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }
}
