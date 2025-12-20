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

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using DTC.Core.Extensions;
using G33kJavu.Core.Models;

namespace G33kJavu.Core.Scanning;

public static class LineNormalizer
{
    private static readonly Regex WhitespaceRegex = new Regex(@"\s+", RegexOptions.Compiled);
    private static readonly Regex DoubleQuotedStringRegex = new Regex("\"(?:\\\\.|[^\"\\\\])*\"", RegexOptions.Compiled);
    private static readonly Regex SingleQuotedStringRegex = new Regex("'(?:\\\\.|[^'\\\\])*'", RegexOptions.Compiled);

    private static readonly Regex NumericRegex = new Regex(@"(?<![A-Za-z_])(?:0x[0-9A-Fa-f]+|\d+(?:\.\d+)?)", RegexOptions.Compiled);

    private static bool LooksLikeCodeString(string quotedLiteral)
    {
        if (string.IsNullOrEmpty(quotedLiteral) || quotedLiteral.Length < 2)
            return false;

        // Strip the surrounding quotes (leave escapes as-is; heuristic only).
        var inner = quotedLiteral.Substring(1, quotedLiteral.Length - 2);
        if (inner.Length == 0)
            return false;

        // Heuristic: if the string looks like code, preserve it to avoid false matches.
        // e.g. "foo();" or "{ x: 1 }" or "(a, b)".
        if (inner.IndexOfAny(['{', '}', '(', ')']) >= 0)
            return true;

        if (inner.TrimEnd().EndsWith(';'))
            return true;

        return false;
    }

    private static bool ShouldPreserveStringLiteral(string fullLine, Match m)
    {
        if (LooksLikeCodeString(m.Value))
            return true;

        // Preserve indexer-style strings: ["blah"] (including whitespace inside).
        var before = m.Index - 1;
        while (before >= 0 && char.IsWhiteSpace(fullLine[before]))
            before--;
        var after = m.Index + m.Length;
        while (after < fullLine.Length && char.IsWhiteSpace(fullLine[after]))
            after++;

        if (before >= 0 && fullLine[before] == '[' && after < fullLine.Length && fullLine[after] == ']')
            return true;

        // Preserve "just a string," list items: "blah blah",
        // i.e. line is a single quoted literal optionally followed by a trailing comma and/or comment.
        var start = 0;
        while (start < fullLine.Length && char.IsWhiteSpace(fullLine[start]))
            start++;

        if (start == m.Index)
        {
            var end = m.Index + m.Length;
            while (end < fullLine.Length && char.IsWhiteSpace(fullLine[end]))
                end++;

            if (end < fullLine.Length && fullLine[end] == ',')
            {
                end++;
                while (end < fullLine.Length && char.IsWhiteSpace(fullLine[end]))
                    end++;

                if (end >= fullLine.Length)
                    return true;

                if (end + 1 < fullLine.Length && fullLine[end] == '/' && fullLine[end + 1] == '/')
                    return true;
            }
        }

        return false;
    }

    public static string? NormalizeLine(string? line, FileCategory category, bool ignoreStringContent, bool ignoreComments)
    {
        if (line == null)
            return null;

        line = line.Trim();
        if (line.Length < 2)
            return null;

        if (ignoreComments)
        {
            if (line.StartsWith("//"))
                return null;
            if (line.StartsWith("/*"))
                return null;
            if (line.StartsWith('*'))
                return null;
            if (line.EndsWith("*/"))
                return null;
        }

        if (ignoreStringContent)
        {
            // Always normalize double-quoted strings as STR (unless they look like code or data we want to preserve).
            line = DoubleQuotedStringRegex.Replace(line, m => ShouldPreserveStringLiteral(line, m) ? m.Value : "STR");

            // Only treat single-quoted literals as strings in languages where that's idiomatic.
            if (category is FileCategory.Python or FileCategory.JavaScript)
                line = SingleQuotedStringRegex.Replace(line, m => ShouldPreserveStringLiteral(line, m) ? m.Value : "STR");
        }

        line = NumericRegex.Replace(line, match => match.Value is "0" or "1" ? match.Value : "123");
        line = WhitespaceRegex.Replace(line, " ").Trim();
        if (line.Length < 2)
            return null;

        return line;
    }

    public static void NormalizeFile(
        FileInfo file,
        FileCategory category,
        bool ignoreStringContent,
        bool ignoreComments,
        List<int> originalLineNumbers,
        List<ulong> lineHashes,
        CancellationToken cancellationToken)
    {
        using var stream = file.OpenRead();
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);

        var originalLineNumber = 0;
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = reader.ReadLine();
            originalLineNumber++;
            if (line == null)
                continue;

            var normalized = NormalizeLine(line, category, ignoreStringContent, ignoreComments);
            if (normalized == null)
                continue;

            originalLineNumbers.Add(originalLineNumber);
            lineHashes.Add(normalized.Fnv1a64());
        }
    }
}
