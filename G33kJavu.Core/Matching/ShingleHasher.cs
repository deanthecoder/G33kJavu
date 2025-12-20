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

namespace G33kJavu.Core.Matching;

internal static class ShingleHasher
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    public static ulong Hash(ReadOnlySpan<ulong> kLineHashes)
    {
        unchecked
        {
            var hash = OffsetBasis;
            for (var i = 0; i < kLineHashes.Length; i++)
            {
                var v = kLineHashes[i];
                for (var b = 0; b < 8; b++)
                {
                    hash ^= (byte)v;
                    hash *= Prime;
                    v >>= 8;
                }
            }

            return hash;
        }
    }
}

