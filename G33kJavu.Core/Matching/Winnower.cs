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
using G33kJavu.Core.Models;

namespace G33kJavu.Core.Matching;

internal static class Winnower
{
    public static List<Fingerprint> Winnow(ReadOnlySpan<ulong> shingleHashes, int w, int fileId)
    {
        var results = new List<Fingerprint>(capacity: shingleHashes.Length / Math.Max(1, w));
        if (w <= 0 || shingleHashes.Length == 0)
            return results;

        // Monotonic queue of candidate minima: indices with non-decreasing hash values.
        var deque = new LinkedList<int>();
        var lastPickedIndex = -1;

        for (var i = 0; i < shingleHashes.Length; i++)
        {
            var current = shingleHashes[i];

            while (deque.Count > 0)
            {
                var lastIndex = deque.Last!.Value;
                var lastValue = shingleHashes[lastIndex];

                if (current < lastValue)
                    deque.RemoveLast();
                else
                    break; // Tie keeps earlier index => deterministic and earliest tie-break.
            }
            deque.AddLast(i);

            var windowStart = i - w + 1;
            if (windowStart < 0)
                continue;

            while (deque.Count > 0 && deque.First!.Value < windowStart)
                deque.RemoveFirst();

            var minIndex = deque.First!.Value;
            if (minIndex != lastPickedIndex)
            {
                lastPickedIndex = minIndex;
                results.Add(new Fingerprint(shingleHashes[minIndex], fileId, minIndex));
            }
        }

        return results;
    }
}

