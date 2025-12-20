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
using System.Linq;
using System.Threading;
using G33kJavu.Core.Models;

namespace G33kJavu.Core.Matching;

internal static class MatchDiscovery
{
    private readonly record struct Seed(int AStart, int BStart);

    private sealed class SeedComparer : IComparer<Seed>
    {
        public static SeedComparer Instance { get; } = new SeedComparer();

        public int Compare(Seed x, Seed y)
        {
            var cmp = x.AStart.CompareTo(y.AStart);
            if (cmp != 0)
                return cmp;
            return x.BStart.CompareTo(y.BStart);
        }
    }

    public static List<DuplicateMatch> FindMatches(
        IReadOnlyList<ProcessedFile> files,
        Dictionary<ulong, List<Occurrence>> index,
        ScanSettings settings,
        Action<int> onMatchCountChanged,
        CancellationToken cancellationToken)
    {
        var fileById = files.ToDictionary(o => o.FileId);

        var matchAccumulator = new Dictionary<(int, int), List<DuplicateMatch>>();
        var seedsByPair = new Dictionary<(int, int), List<(int Delta, Seed Seed)>>();

        var fingerprintHashes = index.Keys.ToList();
        fingerprintHashes.Sort();

        foreach (var fingerprintHash in fingerprintHashes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var occurrences = index[fingerprintHash];
            if (occurrences.Count < 2)
                continue;

            occurrences.Sort(static (a, b) =>
            {
                var cmp = a.FileId.CompareTo(b.FileId);
                if (cmp != 0)
                    return cmp;
                return a.StartLineIndex.CompareTo(b.StartLineIndex);
            });

            for (var i = 0; i < occurrences.Count; i++)
            {
                var left = occurrences[i];
                for (var j = i + 1; j < occurrences.Count; j++)
                {
                    var right = occurrences[j];

                    if (left.FileId == right.FileId)
                        continue;

                    var fileAId = Math.Min(left.FileId, right.FileId);
                    var fileBId = Math.Max(left.FileId, right.FileId);

                    var fileA = fileById[fileAId];
                    var fileB = fileById[fileBId];
                    if (fileA.Category != fileB.Category)
                        continue;

                    var aStart = fileAId == left.FileId ? left.StartLineIndex : right.StartLineIndex;
                    var bStart = fileBId == right.FileId ? right.StartLineIndex : left.StartLineIndex;

                    var delta = aStart - bStart;
                    var key = (fileAId, fileBId);

                    if (!seedsByPair.TryGetValue(key, out var seeds))
                        seedsByPair[key] = seeds = [];

                    seeds.Add((delta, new Seed(aStart, bStart)));
                }
            }
        }

        foreach (var pairKvp in seedsByPair.OrderBy(o => o.Key.Item1).ThenBy(o => o.Key.Item2))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (fileAId, fileBId) = pairKvp.Key;
            var fileA = fileById[fileAId];
            var fileB = fileById[fileBId];

            // Group by diagonal.
            var diagGroups = pairKvp.Value
                .GroupBy(o => o.Delta)
                .OrderBy(o => o.Key);

            foreach (var diagGroup in diagGroups)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var seeds = diagGroup.Select(o => o.Seed).ToList();
                seeds.Sort(SeedComparer.Instance);

                var maxSeedGap = settings.GapAllowance + 1;
                var runStart = seeds[0];
                var prev = seeds[0];

                for (var i = 1; i < seeds.Count; i++)
                {
                    var current = seeds[i];
                    if (current.AStart <= prev.AStart + maxSeedGap)
                    {
                        prev = current;
                        continue;
                    }

                    AddCandidate(fileA, fileB, runStart, prev, settings, matchAccumulator, onMatchCountChanged);
                    runStart = prev = current;
                }

                AddCandidate(fileA, fileB, runStart, prev, settings, matchAccumulator, onMatchCountChanged);
            }
        }

        var results = new List<DuplicateMatch>();
        foreach (var pairMatches in matchAccumulator.OrderBy(o => o.Key.Item1).ThenBy(o => o.Key.Item2))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var merged = DeDuplicateAndCullNested(pairMatches.Value);
            results.AddRange(merged);
        }

        results.Sort(static (a, b) =>
        {
            var cmp = b.MatchedLineCount.CompareTo(a.MatchedLineCount);
            if (cmp != 0)
                return cmp;
            cmp = a.FileAId.CompareTo(b.FileAId);
            if (cmp != 0)
                return cmp;
            cmp = a.FileBId.CompareTo(b.FileBId);
            if (cmp != 0)
                return cmp;
            cmp = a.AStartOriginalLineNumber.CompareTo(b.AStartOriginalLineNumber);
            if (cmp != 0)
                return cmp;
            return a.BStartOriginalLineNumber.CompareTo(b.BStartOriginalLineNumber);
        });

        return results;
    }

    private static void AddCandidate(
        ProcessedFile fileA,
        ProcessedFile fileB,
        Seed first,
        Seed last,
        ScanSettings settings,
        Dictionary<(int, int), List<DuplicateMatch>> matchAccumulator,
        Action<int> onMatchCountChanged)
    {
        var k = Math.Max(1, settings.K);

        var aStart = first.AStart;
        var bStart = first.BStart;
        var aEnd = last.AStart + k - 1;
        var bEnd = last.BStart + k - 1;

        ExpandToMaximal(fileA, fileB, ref aStart, ref aEnd, ref bStart, ref bEnd);

        var matchedCount = aEnd - aStart + 1;
        if (matchedCount < settings.MinReportLines)
            return;

        if (aStart < 0 || bStart < 0 || aEnd >= fileA.NormalizedLineCount || bEnd >= fileB.NormalizedLineCount)
            return;

        var match = new DuplicateMatch
        {
            FileAId = fileA.FileId,
            FileBId = fileB.FileId,
            AStartNormLine = aStart,
            AEndNormLine = aEnd,
            BStartNormLine = bStart,
            BEndNormLine = bEnd,
            MatchedLineCount = matchedCount,
            AStartOriginalLineNumber = fileA.OriginalLineNumbers[aStart],
            AEndOriginalLineNumber = fileA.OriginalLineNumbers[aEnd],
            BStartOriginalLineNumber = fileB.OriginalLineNumbers[bStart],
            BEndOriginalLineNumber = fileB.OriginalLineNumbers[bEnd],
            HashId = StableId(fileA.FileId, fileB.FileId,
                fileA.OriginalLineNumbers[aStart], fileA.OriginalLineNumbers[aEnd],
                fileB.OriginalLineNumbers[bStart], fileB.OriginalLineNumbers[bEnd])
        };

        var key = (match.FileAId, match.FileBId);
        if (!matchAccumulator.TryGetValue(key, out var list))
            matchAccumulator[key] = list = [];

        list.Add(match);
        onMatchCountChanged?.Invoke(list.Count);
    }

    private static void ExpandToMaximal(ProcessedFile fileA, ProcessedFile fileB, ref int aStart, ref int aEnd, ref int bStart, ref int bEnd)
    {
        var aHashes = fileA.LineHashes;
        var bHashes = fileB.LineHashes;

        while (aStart > 0 && bStart > 0 && aHashes[aStart - 1] == bHashes[bStart - 1])
        {
            aStart--;
            bStart--;
        }

        while (aEnd + 1 < aHashes.Length && bEnd + 1 < bHashes.Length && aHashes[aEnd + 1] == bHashes[bEnd + 1])
        {
            aEnd++;
            bEnd++;
        }
    }

    private static ulong StableId(int fileAId, int fileBId, int aStartOriginal, int aEndOriginal, int bStartOriginal, int bEndOriginal)
    {
        unchecked
        {
            var hash = 14695981039346656037UL;
            hash = (hash ^ (uint)fileAId) * 1099511628211UL;
            hash = (hash ^ (uint)fileBId) * 1099511628211UL;
            hash = (hash ^ (uint)aStartOriginal) * 1099511628211UL;
            hash = (hash ^ (uint)aEndOriginal) * 1099511628211UL;
            hash = (hash ^ (uint)bStartOriginal) * 1099511628211UL;
            hash = (hash ^ (uint)bEndOriginal) * 1099511628211UL;
            return hash;
        }
    }

    private static List<DuplicateMatch> DeDuplicateAndCullNested(List<DuplicateMatch> matches)
    {
        var uniqueByOriginalRange = matches
            .GroupBy(o => (o.FileAId, o.FileBId, o.AStartOriginalLineNumber, o.AEndOriginalLineNumber, o.BStartOriginalLineNumber, o.BEndOriginalLineNumber))
            .Select(o => o.First())
            .ToList();

        uniqueByOriginalRange.Sort(static (a, b) => b.MatchedLineCount.CompareTo(a.MatchedLineCount));

        var results = new List<DuplicateMatch>();
        for (var i = 0; i < uniqueByOriginalRange.Count; i++)
        {
            var candidate = uniqueByOriginalRange[i];
            var isNested = results.Any(existing =>
                candidate.AStartNormLine >= existing.AStartNormLine &&
                candidate.AEndNormLine <= existing.AEndNormLine &&
                candidate.BStartNormLine >= existing.BStartNormLine &&
                candidate.BEndNormLine <= existing.BEndNormLine);

            if (!isNested)
                results.Add(candidate);
        }

        results.Sort(static (a, b) =>
        {
            var cmp = b.MatchedLineCount.CompareTo(a.MatchedLineCount);
            if (cmp != 0)
                return cmp;
            cmp = a.AStartOriginalLineNumber.CompareTo(b.AStartOriginalLineNumber);
            if (cmp != 0)
                return cmp;
            return a.BStartOriginalLineNumber.CompareTo(b.BStartOriginalLineNumber);
        });

        return results;
    }
}
