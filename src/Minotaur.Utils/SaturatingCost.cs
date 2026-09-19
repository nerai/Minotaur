using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Minotaur.Utils;

/// <summary>
/// Saturating unsigned integer
/// 
/// Supports addition, multiplication, min/max and comparisons.
/// 
/// In comparisons, infinity is considered a number:
/// inf < inf: false
/// inf <= inf: true
/// </summary>
public readonly record struct SaturatingCost : IComparable<SaturatingCost>
{
	private readonly UInt64 U;

	public static readonly SaturatingCost Zero = new SaturatingCost (0);
	public static readonly SaturatingCost Infinity = new SaturatingCost (UInt64.MaxValue);

	public bool IsInfinity => U == UInt64.MaxValue;

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	[DebuggerStepThrough]
	public SaturatingCost (ulong init)
	{
		U = init;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	[DebuggerStepThrough]
	public static implicit operator SaturatingCost (ulong init)
	{
		return new SaturatingCost (init);
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	[DebuggerStepThrough]
	public static bool operator < (SaturatingCost lhs, SaturatingCost rhs)
	{
		return lhs.U < rhs.U;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	[DebuggerStepThrough]
	public static bool operator > (SaturatingCost lhs, SaturatingCost rhs)
	{
		return lhs.U > rhs.U;
	}

	[DebuggerStepThrough]
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static bool operator <= (SaturatingCost lhs, SaturatingCost rhs)
	{
		return lhs.U <= rhs.U;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	[DebuggerStepThrough]
	public static bool operator >= (SaturatingCost lhs, SaturatingCost rhs)
	{
		return lhs.U >= rhs.U;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	[DebuggerStepThrough]
	public static SaturatingCost operator + (SaturatingCost lhs, SaturatingCost rhs)
	{
		ulong u = unchecked(lhs.U + rhs.U);
		if (u < lhs) {
			// Overflow
			return Infinity;
		};
		return u;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	[DebuggerStepThrough]
	public static SaturatingCost operator * (SaturatingCost lhs, uint scale)
	{
		ulong hi = Math.BigMul (lhs.U, (ulong) scale, out ulong lo);
		if (hi > 0) {
			// Overflow
			return Infinity;
		}
		return lo;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	[DebuggerStepThrough]
	public static SaturatingCost Sum (IEnumerable<SaturatingCost> its)
	{
		var sum = Zero;
		foreach (var it in its) {
			sum += it;
		}
		return sum;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	[DebuggerStepThrough]
	public static SaturatingCost Min (SaturatingCost lhs, SaturatingCost rhs)
	{
		return lhs.U < rhs.U ? lhs : rhs;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	[DebuggerStepThrough]
	public static SaturatingCost Max (SaturatingCost lhs, SaturatingCost rhs)
	{
		return lhs.U > rhs.U ? lhs : rhs;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	[DebuggerStepThrough]
	public static SaturatingCost Min (IEnumerable<SaturatingCost> its)
	{
		var min = Infinity;
		foreach (var it in its) {
			if (it < min) {
				min = it;
			}
		}
		return min;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	[DebuggerStepThrough]
	public static SaturatingCost Max (IEnumerable<SaturatingCost> its)
	{
		var max = Zero;
		foreach (var it in its) {
			if (it > max) {
				max = it;
			}
		}
		return max;
	}

	[DebuggerStepThrough]
	public override string ToString ()
	{
		if (IsInfinity) {
			return "£inf";
		}
		return $"£{U}";
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	[DebuggerStepThrough]
	public int CompareTo (SaturatingCost other)
	{
		if (this < other) { return -1; }
		if (this > other) { return +1; }
		return 0;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	[DebuggerStepThrough]
	public SaturatingCost ReduceBy (SaturatingCost by)
	{
		if (by > U) {
			throw new ArgumentOutOfRangeException (nameof (by), "Cannot reduce below 0.");
		}
		var uu = checked(U - by.U);
		return new SaturatingCost (uu);
	}

	public ulong AsUlong () => U;
}
