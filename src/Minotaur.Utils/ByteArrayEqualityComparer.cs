using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Minotaur.Utils;

/// <summary>
/// This class assumes that a GOOD hash of the array is stored in the first 4 bytes.
/// </summary>
public class ByteArrayEqualityComparer : IEqualityComparer<ImmutableArray<byte>>
{
	public static readonly ByteArrayEqualityComparer Instance = new ();

	public bool Equals (ImmutableArray<byte> x, ImmutableArray<byte> y)
	{
		if (x.Length != y.Length) {
			return false;
		}
		for (int i = x.Length - 1; i >= 0; i--) {
			if (x [i] != y [i]) {
				return false;
			}
		}
		return true;
	}

	public int GetHashCode (ImmutableArray<byte> a)
	{
		return 0
			| (a [0] << 24)
			| (a [1] << 16)
			| (a [2] << 8)
			| (a [3] << 0);
	}
}

public class IntArrayEqualityComparer : IEqualityComparer<ImmutableArray<int>>
{
	public static readonly IntArrayEqualityComparer Instance = new ();

	public bool Equals (ImmutableArray<int> x, ImmutableArray<int> y)
	{
		if (x.Length != y.Length) {
			return false;
		}
		for (int i = x.Length - 1; i >= 0; i--) {
			if (x [i] != y [i]) {
				return false;
			}
		}
		return true;
	}

	public int GetHashCode (ImmutableArray<int> a)
	{
		return a [0];
	}
}
