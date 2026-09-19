using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using Minotaur.Utils;

namespace Minotaur.Boards.Strings;

// Class is called Worm because String was already taken, sorry
public sealed class Worm : IEquatable<Worm> // TODO IDisposable
{
	public readonly ImmutableArray<int> Cells;

	internal readonly SmallSet<Worm> _Neighbors;
	private readonly SmallSet<Worm> _Disallowed;

	public Worm (int cell, WormyBoardGrid board)
	{
		Cells = ImmutableArray.Create (cell);
		_Disallowed = new (0);
		_Neighbors = new (4);
	}

	public void InitNeighbors (int cell, WormyBoardGrid board)
	{
		int x = cell % board.SX;
		int y = cell / board.SX;

		void add (int c)
		{
			var neighbour = board.WormInCell [c];
			if (neighbour != null) {
				_Neighbors.Add (neighbour);
				neighbour._Neighbors.Add (this);
			}
		}
		if (x > 0) {
			add (cell - 1);
		}
		if (y > 0) {
			add (cell - board.SX);
		}
	}

	/// <summary>
	/// Update neighbors to the current board state.
	/// 
	/// If checkDupes is cleared, do not check for duplicates.
	/// Use this when it is known that there are no dupes.
	/// </summary>
	public void Refresh (WormyBoardGrid board, bool checkDupes)
	{
		{
			var e = _Neighbors.GetMutableEnumerator ();
			while (e.MoveNext ()) {
				e.Current = board.WormInCell [e.Current.Cells [0]];
			}
		}
		{
			var e = _Disallowed.GetMutableEnumerator ();
			while (e.MoveNext ()) {
				e.Current = board.WormInCell [e.Current.Cells [0]];
			}
		}

		if (checkDupes) {
			_Neighbors.DiscardDuplicates ();
			_Disallowed.DiscardDuplicates ();
		}
		else {
			Debug.Assert (_Neighbors.DiscardDuplicates () == 0);
			Debug.Assert (_Disallowed.DiscardDuplicates () == 0);
		}
	}

	public Worm (Worm clone)
	{
		Cells = clone.Cells;
		_Neighbors = new (clone._Neighbors);
		_Disallowed = new (clone._Disallowed);
	}

	/// <summary>
	/// Create merged worm.
	/// </summary>
	public Worm (Worm w1, Worm w2)
	{
		/*
		 * Merge cells
		 */
		var cb = w1.Cells.ToBuilder ();
		cb.AddRange (w2.Cells);
		Cells = cb.ToImmutableArray ();
		if (Cells.Length > 5) {
			throw new InvalidOperationException ();
		}

		/*
		 * Merge the neighbour cell sets, but remove those that are inside
		 */
		_Neighbors = SmallSet<Worm>.Merge (w1._Neighbors, w2._Neighbors);
		_Neighbors.Remove (w1);
		_Neighbors.Remove (w2);

		/*
		 * Merge disallowed cells
		 */
		_Disallowed = SmallSet<Worm>.Merge (w1._Disallowed, w2._Disallowed);
	}

	public void SplitOffCompletedStringsMutually ()
	{
		/*
		 * Remove all connections where the merged cell count would get too high.
		 * This includes the special case where this new worm consists of 5 cells.
		 */
		foreach (var nb in _Neighbors) {
			if (Cells.Length + nb.Cells.Length > 5) {
				_Neighbors.Remove (nb);
				_Disallowed.Remove (nb);
				nb._Neighbors.Remove (this);
				nb._Disallowed.Remove (this);
			}
		}
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public bool Equals (Worm other)
	{
		return this == other;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public void UniqueWormHash (WormyBoardGrid grid, ImmutableArray<byte>.Builder b)
	{
		/*
		 * This can only handle up to 254 cells
		 */
		Debug.Assert (grid.WormInCell.Length <= 254);

		b.Add (255);
		{
			var cs = Cells.OrderBy (c => c);
			foreach (var c in cs) {
				b.Add ((byte) c);
			}
		}
		b.Add (255);
		{
			var nbs = _Neighbors.OrderBy (nb => nb.Cells.Min ());
			foreach (var nb in nbs) {
				b.Add ((byte) nb.Cells.Min ());
			}
		}
		b.Add (255);
		{
			var cs = _Disallowed
				.Select (nb => nb.Cells.Min ())
				.OrderBy (c => c);
			foreach (var c in cs) {
				b.Add ((byte) c);
			}
		}
	}

	/// <summary>
	/// Remember the other worm is disallowed.
	/// </summary>
	internal void DisallowInplace (Worm bad)
	{
		Debug.Assert (!_Disallowed.Contains (bad));

		_Disallowed.Add (bad);
	}

	public bool IsNotAllowedToConnectTo (Worm w)
	{
		var ret = _Disallowed.Contains (w);
		Debug.Assert (ret == w._Disallowed.Contains (this));
		return ret;
	}

	public override string ToString ()
	{
		var sb = new StringBuilder ();

		var mycs = Cells.OrderBy (c => c);
		sb.Append ($"[{string.Join (",", mycs)}] (");

		var nbcs = _Neighbors
			.SelectMany (nb => nb.Cells)
			.OrderBy (c => c);
		sb.Append (string.Join (",", nbcs));

		if (_Disallowed.Any ()) {
			var dcs = _Disallowed
				.SelectMany (nb => nb.Cells)
				.OrderBy (c => c);
			sb.Append ($" ^");
			sb.Append (string.Join (",", dcs));
		}

		sb.Append ($")");
		return sb.ToString ();
	}

	public string ToShortString ()
	{
		return string.Join (",", Cells.OrderBy (c => c));
	}

	public string Latex ()
	{
		var cells = string.Join (",", Cells.OrderBy (i => i));
		return $"\\str{{{cells}}}";
	}

	public bool HasNeighbour (Worm w)
	{
		return _Neighbors.Contains (w);
	}

	public SmallSet<Worm> NeighbourWorms_OnlyAllowed ()
	{
		SmallSet<Worm> nbs = new (_Neighbors);
		nbs.RemoveWhere (nb => _Disallowed.Contains (nb));
		Debug.Assert (nbs.All (nb => Cells.Length + nb.Cells.Length <= 5));
		return nbs;
	}

	public (int worms, int cells) CountReachableAllowedInDistance (int maxStepsOfBFS)
	{
		if (maxStepsOfBFS < 0) {
			throw new ArgumentOutOfRangeException (nameof (maxStepsOfBFS));
		}
		if (maxStepsOfBFS == 0) {
			return (1, Cells.Length);
		}

		SmallSet<Worm> reachable = new (_Neighbors);
		for (int i = 2; i <= maxStepsOfBFS; i++) {
			reachable.ReplaceMany (w => w.NeighbourWorms_OnlyAllowed ());
		}
		return (reachable.Count, reachable.Sum (w => w.Cells.Length));
	}

	/*
	/// <summary>
	/// Find all worms that are reachable from this worm.
	/// This worm itself is included in the results.
	/// </summary>
	/// <param name="excludeFull">
	/// Do not allow connecting to completed worms (size 5).
	/// </param>
	public SmallSet<Worm> ReachableWorms (
		WormyBoardGrid board,
		bool excludeFull)
	{
		var set = new SmallSet<Worm> (board.WormInCell.Length);
		var work = new Queue<Worm> ();
		work.Enqueue (this);

		while (work.Any ()) {
			var w = work.Dequeue ();
			if (!set.Add (w)) {
				// Node already known, no need to parse again
				continue;
			}
			// Add children
			foreach (var n in w.NeighbourWorms_IncludingDisallowed (board)) {
				if (excludeFull && n.Cells.Count >= 5) {
					continue;
				}
				work.Enqueue (n);
			}
		}

		return set;
	}
	*/
}
