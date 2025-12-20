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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using G33kJavu.Core.Matching;
using G33kJavu.Core.Models;
using G33kJavu.Core.Scanning;

namespace G33kJavu.Core.Services;

public sealed class ScanService
{
    public async Task<ScanResult> ScanAsync(
        DirectoryInfo root,
        ScanSettings settings,
        IProgress<ScanProgress> progress,
        CancellationToken cancellationToken)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        progress?.Report(new ScanProgress(ScanPhase.Enumerating, 0, 0, 0));
        await Task.Yield();

        var filesToProcess = FileEnumerator.Enumerate(root);
        progress?.Report(new ScanProgress(ScanPhase.Normalizing, 0, filesToProcess.Count, 0));

        var processedFiles = new List<ProcessedFile>(filesToProcess.Count);

        for (var i = 0; i < filesToProcess.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileToProcess = filesToProcess[i];
            var originalLineNumbers = new List<int>(capacity: 2048);
            var lineHashes = new List<ulong>(capacity: 2048);

            try
            {
                LineNormalizer.NormalizeFile(fileToProcess.File, fileToProcess.Category, settings.IgnoreStringContent, settings.IgnoreComments, originalLineNumbers, lineHashes, cancellationToken);
            }
            catch
            {
                // Skip unreadable files.
                continue;
            }

            if (lineHashes.Count < Math.Max(settings.K, settings.MinReportLines))
            {
                progress?.Report(new ScanProgress(ScanPhase.Normalizing, i + 1, filesToProcess.Count, 0));
                continue;
            }

            var normalizedLineHashes = lineHashes.ToArray();
            processedFiles.Add(new ProcessedFile
            {
                FileId = processedFiles.Count,
                Path = fileToProcess.File,
                Category = fileToProcess.Category,
                ContentHash = ContentHasher.Hash(normalizedLineHashes),
                OriginalLineNumbers = originalLineNumbers.ToArray(),
                LineHashes = normalizedLineHashes
            });

            progress?.Report(new ScanProgress(ScanPhase.Normalizing, i + 1, filesToProcess.Count, 0));
        }

        progress?.Report(new ScanProgress(ScanPhase.Fingerprinting, 0, processedFiles.Count, 0));

        var index = new Dictionary<ulong, List<Occurrence>>(capacity: processedFiles.Count * 128);

        for (var fileIndex = 0; fileIndex < processedFiles.Count; fileIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file = processedFiles[fileIndex];
            var k = Math.Max(1, settings.K);
            var w = Math.Max(1, settings.W);

            var shinglesCount = file.LineHashes.Length - k + 1;
            if (shinglesCount <= 0)
            {
                progress?.Report(new ScanProgress(ScanPhase.Fingerprinting, fileIndex + 1, processedFiles.Count, 0));
                continue;
            }

            var shingles = new ulong[shinglesCount];
            for (var i = 0; i < shinglesCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                shingles[i] = ShingleHasher.Hash(file.LineHashes.AsSpan(i, k));
            }

            var fingerprints = Winnower.Winnow(shingles, w, file.FileId);
            for (var i = 0; i < fingerprints.Count; i++)
            {
                var fp = fingerprints[i];
                if (!index.TryGetValue(fp.Hash, out var occurrences))
                    index[fp.Hash] = occurrences = [];
                occurrences.Add(new Occurrence(fp.FileId, fp.StartLineIndex));
            }

            progress?.Report(new ScanProgress(ScanPhase.Fingerprinting, fileIndex + 1, processedFiles.Count, 0));
        }

        // Reduce boilerplate fingerprints.
        var toRemove = index.Where(o => o.Value.Count > settings.MaxOccurrencesPerFingerprint).Select(o => o.Key).ToList();
        for (var i = 0; i < toRemove.Count; i++)
            index.Remove(toRemove[i]);

        progress?.Report(new ScanProgress(ScanPhase.Matching, 0, processedFiles.Count, 0));

        var matchesFound = 0;
        var matches = await Task.Run(() =>
            MatchDiscovery.FindMatches(
                processedFiles,
                index,
                settings,
                _ =>
                {
                    matchesFound++;
                    progress?.Report(new ScanProgress(ScanPhase.Matching, processedFiles.Count, processedFiles.Count, matchesFound));
                },
                cancellationToken), cancellationToken);

        progress?.Report(new ScanProgress(ScanPhase.Finalizing, processedFiles.Count, processedFiles.Count, matches.Count));

        return new ScanResult
        {
            Files = processedFiles,
            Matches = matches
        };
    }
}
