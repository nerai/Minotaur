using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Minotaur.Utils;

public static class Hashing
{
	// Simple, very fast hash
	[MethodImpl (MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static uint SDBM (string s)
	{
		uint hash = 0;
		unchecked {
			foreach (char c in s) {
				hash = c + (hash << 6) + (hash << 16) - hash;
			}
		}
		return hash;
	}

	// Simple, very fast hash
	[MethodImpl (MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static uint SDBM (in ReadOnlySpan<byte> s)
	{
		uint hash = 0;
		unchecked {
			foreach (byte b in s) {
				hash = b + (hash << 6) + (hash << 16) - hash;
			}
		}
		return hash;
	}

	// Simple, very fast hash
	[MethodImpl (MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static uint SDBM (in ImmutableArray<byte>.Builder s)
	{
		uint hash = 0;
		unchecked {
			foreach (byte b in s) {
				hash = b + (hash << 6) + (hash << 16) - hash;
			}
		}
		return hash;
	}

	/// <summary>
	/// Two round 32 bit integer hash.
	/// This was optimized to have low avalanche bias;
	/// it outperforms the finalizer of MurmurHash3.
	/// Adapted from https://github.com/skeeto/hash-prospector
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static UInt32 Hash32 (UInt32 x)
	{
		unchecked {
			x ^= x >> 15;
			x *= 0xD168AAADu;
			x ^= x >> 15;
			x *= 0xAF723597u;
			x ^= x >> 15;
		}
		return x;
	}

	/// <summary>
	/// Two round 64 bit integer hash.
	/// This was optimized to have low avalanche bias;
	/// it outperforms the finalizer of MurmurHash3.
	/// Adapted from https://github.com/skeeto/hash-prospector
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static UInt64 Hash64 (UInt64 x)
	{
		unchecked {
			x ^= x >> 30;
			x *= 0xbf58476d1ce4e5b9U;
			x ^= x >> 27;
			x *= 0x94d049bb133111ebU;
			x ^= x >> 31;
		}
		return x;
	}
}
