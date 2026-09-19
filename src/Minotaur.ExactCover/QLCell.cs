using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Minotaur.ExactCover;

// Quadruply linked list of a 2d matrix (torus)
public class QLCell
{
	internal QLCell L;
	internal QLCell R;
	internal QLCell U;
	internal QLCell D;
	public ColHead Head;
	public uint RowIndex;

	//private static long _Taken = 0;
	//private static long _Returned = 0;

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	internal static QLCell GetFromPool (Stack<QLCell> _Pool)
	{
		if (_Pool == null) {
			_Pool = new Stack<QLCell> ();
		}
		/*
		var a = Interlocked.Increment (ref _Taken);
		if ((a & 0xFFFF) == 0) {
			Console.WriteLine ($"{Thread.CurrentThread.ManagedThreadId,4}> TAKE {_Taken}, RETURN {_Returned}");
		}
		*/
		if (_Pool.Count == 0) {
			return new QLCell ();
		}
		var c = _Pool.Pop ();
		c.U = c;
		c.D = c;
		c.L = c;
		c.R = c;
		c.Head = null;
		c.RowIndex = uint.MaxValue;
		return c;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	internal static void ReturnToPool (QLCell c, Stack<QLCell> _Pool)
	{
		Debug.Assert (!(c is ColHead));
		const int NodeSize = 40;
		const int MaxCacheSize = 128 * 1024 * 1024;
		const int MaxNodes = MaxCacheSize / NodeSize;
		if (_Pool.Count >= MaxNodes) {
			// Pool is pretty large, don't store this node to keep RAM low
			/*
			lock (Console.Out) {
				Console.WriteLine ("INFORMATION: Node pool exhausted, resetting");
			}
			*/
			return;
		}
		//Interlocked.Increment (ref _Returned);
		_Pool.Push (c);
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	internal QLCell ()
	{
		U = this;
		D = this;
		L = this;
		R = this;
		Head = null;
		RowIndex = uint.MaxValue;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public IEnumerable<QLCell> EnumerateRow ()
	{
		var node = this;
		do {
			yield return node;
			node = node.R;
		}
		while (node != this);
	}

	public override string ToString ()
	{
		return $"{Head}/{RowIndex}";
	}
}
