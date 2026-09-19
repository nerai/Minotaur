using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using Minotaur.Boards.Common;
using Minotaur.Utils;

namespace Minotaur.Boards.Strings;

public sealed class WormyBoardGrid : ISolutionGrid
{
	public readonly int SX;

	public readonly int SY;

	public (int SX, int SY) Size => (SX, SY);

	// TODO das sind unfinished worms, nicht unfinished cells?!? bugbug
	public int UnfinishedCells => WormInCell.Count (w => (w != null) && (w.Cells.Length < 5));

	public int TotalOpenCellCount => WormInCell.Count (w => w != null);

	public readonly SmallSet<Worm> AllWorms;

	public readonly Worm [] WormInCell;

	private ImmutableArray<byte> _BoardHash;

	public ImmutableArray<byte> GetUniqueHash ()
	{
		if (_BoardHash.IsDefault) {
			// Usual worst case: few closed cells, 3 markers, 1 cell, 4 neighbours = 8
			var b = ImmutableArray.CreateBuilder<byte> (4 + WormInCell.Length * 8);

			b.Add (0);
			b.Add (0);
			b.Add (0);
			b.Add (0);

			for (int i = 0; i < WormInCell.Length; i++) {
				var w = WormInCell [i];
				if (w == null) {
					continue;
				}
				if (w.Cells.Min () != i) { // XXX cells sortiert halten!
					continue;
				}
				w.UniqueWormHash (this, b);
			}

			var hash = Hashing.SDBM (b);
			b [0] = (byte) (hash >> 24);
			b [1] = (byte) (hash >> 16);
			b [2] = (byte) (hash >> 8);
			b [3] = (byte) (hash >> 0);

			_BoardHash = b.ToImmutable ();
		}

		return _BoardHash;
	}

	public WormyBoardGrid (BoardGrid from)
	{
		SX = from.SX;
		SY = from.SY;
		WormInCell = new Worm [SX * SY];
		AllWorms = new (SX * SY);

		for (int y = 0; y < SY; y++) {
			for (int x = 0; x < SX; x++) {
				if (from.IsClosedAt (x, y)) {
					if (from.CellAt (x, y) < char.MaxValue) {
						throw new NotImplementedException (); // todo piece liegt dort, das übersetzen
					}
					continue;
				}
				var i = x + y * SX;
				WormInCell [i] = new Worm (i, this);
				AllWorms.Add (WormInCell [i]);
			}
		}

		for (int y = 0; y < SY; y++) {
			for (int x = 0; x < SX; x++) {
				var i = x + y * SX;
				var w = WormInCell [i];
				if (w == null) {
					continue;
				}
				w.InitNeighbors (i, this);
			}
		}
	}

	public WormyBoardGrid (WormyBoardGrid from)
	{
		SX = from.SX;
		SY = from.SY;
		WormInCell = new Worm [SX * SY];
		AllWorms = new (from.AllWorms.Count + 1);

		foreach (var old in from.AllWorms) {
			var add = new Worm (old);
			AllWorms.Add (add);
			foreach (var cell in add.Cells) {
				WormInCell [cell] = add;
			}
		}

		foreach (var w in AllWorms) {
			w.Refresh (this, false);
		}
	}

	public void MergeWormsInplace (IReadOnlyList<Worm> worms, out Worm mergedWorm)
	{
		/*
		 * NOTE: The parameter worms may be from a different board instance!
		 * They have to be translated before use!
		 */

		if (worms.Count < 2) {
			throw new ArgumentException ("komisch");
		}

		/*
		 * Merge all worms.
		 */
		mergedWorm = worms [0];
		for (int i = 1; i < worms.Count; i++) {
			var with = worms [i];
			mergedWorm = new Worm (mergedWorm, with);
		}

		/*
		 * Replace new worm into grid.
		 */
		foreach (var w in worms) {
			var old = WormInCell [w.Cells [0]];
			var ok = AllWorms.Remove (old);
			Debug.Assert (ok);
		}
		var ok2 = AllWorms.Add (mergedWorm);
		Debug.Assert (ok2);

		foreach (var c in mergedWorm.Cells) {
			WormInCell [c] = mergedWorm;
		}

		mergedWorm.Refresh (this, true);
		foreach (var nb in mergedWorm._Neighbors) {
			// This refers to the refreshed nb already
			nb.Refresh (this, true);
		}

		mergedWorm.SplitOffCompletedStringsMutually ();
	}

	public void SplitWormsInplace (Worm w1, Worm w2)
	{
		/*
		 * NOTE: The parameter worms may be from a different board instance!
		 * They have to be translated before use!
		 */

		w1 = WormInCell [w1.Cells [0]];
		w2 = WormInCell [w2.Cells [0]];

		/*
		 * Update the two worms.
		 */
		w1.DisallowInplace (w2);
		w2.DisallowInplace (w1);
	}

	public void SplitBoardInplace (
		Worm cut,
		ImmutableArray<Worm> reachable,
		bool cutIsInSet)
	{
		/*
		 * NOTE: The parameter worms may be from a different board instance!
		 * They have to be translated before use!
		 */

		foreach (var nb in cut._Neighbors) {
			if (cut.IsNotAllowedToConnectTo (nb)) {
				// This nb was not allowed anyway
				continue;
			}
		}

		foreach (var nb in cut._Neighbors) {
			if (cut.IsNotAllowedToConnectTo (nb)) {
				// This nb was not allowed anyway
				continue;
			}
			if (cutIsInSet == reachable.Contains (nb)) {
				/*
				 * The restrictor AND this neigbor are on the same side of the split.
				 * This means the neighbor is still allowed.
				 */
				continue;
			}

			// This nb is no longer allowed
			SplitWormsInplace (cut, nb);
		}
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public bool IsClosedAt (int x, int y)
	{
		var i = x + y * SX;
		return WormInCell [i] == null;
	}

	public string TextualRepresentation (BoardGrid.TextualRepresentationStyle style, List<int> highlight = null)
	{
		// todo style beachten

		var sb = new StringBuilder ();

		for (int y = 0; y < SY; y++) {
			var nextRow = new StringBuilder ();
			for (int x = 0; x < SX; x++) {
				var i = x + y * SX;
				var me = WormInCell [i];
				var rig = x >= SX - 1 ? null : WormInCell [i + 1];
				var bot = y >= SY - 1 ? null : WormInCell [i + SX];

				if (me == null) {
					sb.Append ($"  ");
				}
				else {
					sb.Append ($"{i:00}");
					//sb.Append ($":{string.Join (",", me.Neighbours.Select (n => n.Cells.First ()))}");
				}

				if (me != null && me == rig) {
					sb.Append ($"==");
				}
				else if (true
					&& me != null
					&& rig != null
					&& me.Cells.Length < 5
					&& rig.Cells.Length < 5
					&& !me.IsNotAllowedToConnectTo (rig)
					) {
					sb.Append ($"..");
				}
				else {
					sb.Append ($"  ");
				}

				if (me != null && me == bot) {
					nextRow.Append ("||"); // todo \u2193\u2191
				}
				else if (true
					&& me != null
					&& bot != null
					&& me.Cells.Length < 5
					&& bot.Cells.Length < 5
					&& !me.IsNotAllowedToConnectTo (bot)
					) {
					nextRow.Append ($" :");
				}
				else {
					nextRow.Append ($"  ");
				}
				nextRow.Append ("  ");
			}
			sb.Append ($"\n{nextRow.ToString ()}\n");
		}

		return sb.ToString ();
	}

	private static readonly ConsoleColor [] _ConsoleColorList = new ConsoleColor [] {
		//ConsoleColor.Red,
		ConsoleColor.Green,
		//ConsoleColor.Blue,
		ConsoleColor.Yellow,
		ConsoleColor.Cyan,
		ConsoleColor.Magenta,
	};

	public List<object> TextualRepresentationEx (Worm [] highlight)
	{
		var res = new List<object> ();

		var map = new Dictionary<Worm, ConsoleColor> ();
		ConsoleColor GetColor (Worm w)
		{
			if (w == null) {
				return ConsoleColor.White;
			}
			if (!map.TryGetValue (w, out var col)) {
				var counts = _ConsoleColorList.ToDictionary (c => c, c => 0);
				foreach (var n in w._Neighbors) {
					if (map.TryGetValue (n, out var ncol)) {
						counts [ncol]++;
					}
				}
				// Pick first color (in order) of the color list which has a minimum occurrence count near this worm
				var min = counts.Values.Min ();
				col = _ConsoleColorList
					.Where (c => counts [c] == min)
					.OrderBy (c => map.Values.Count (v => v == c))
					.First ();
				map.Add (w, col);
			}
			return col;
		}

		for (int y = 0; y < SY; y++) {
			var nextRow = new List<object> ();
			for (int x = 0; x < SX; x++) {
				var i = x + y * SX;
				var me = WormInCell [i];
				var rig = x >= SX - 1 ? null : WormInCell [i + 1];
				var bot = y >= SY - 1 ? null : WormInCell [i + SX];

				if (me == null) {
					res.Add ($"  ");
				}
				else {
					if (me.Cells.Length == 5) {
						res.Add (ConsoleColor.DarkGray);
					}
					else {
						res.Add (ConsoleColor.White);
					}
					if (highlight.Contains (me)) {
						res.Add (Logging.Background);
						res.Add (ConsoleColor.DarkBlue);
						res.Add ($"{i:00}");
						res.Add (Logging.ResetColor);
					}
					else {
						res.Add ($"{i:00}");
					}
					//sb.Append ($":{string.Join (",", me.Neighbours.Select (n => n.Cells.First ()))}");
				}

				if (me != null && me == rig) {
					res.Add (GetColor (me));
					res.Add ($"==");
				}
				else if (me != null && rig != null && me.HasNeighbour (rig)) {
					if (me.IsNotAllowedToConnectTo (rig)) {
						res.Add (ConsoleColor.Red);
					}
					else {
						if (me.Cells.Length == 5 || rig.Cells.Length == 5) {
							res.Add (ConsoleColor.DarkGray);
						}
						else {
							res.Add (ConsoleColor.White);
						}
					}
					res.Add ($"..");
				}
				else {
					res.Add ($"  ");
				}

				if (me != null && me == bot) {
					nextRow.Add (GetColor (me));
					nextRow.Add ("||"); // todo \u2193\u2191
				}
				else if (me != null && bot != null && me.HasNeighbour (bot)) {
					if (me.IsNotAllowedToConnectTo (bot)) {
						nextRow.Add (ConsoleColor.Red);
					}
					else {
						if (me.Cells.Length == 5 || bot.Cells.Length == 5) {
							nextRow.Add (ConsoleColor.DarkGray);
						}
						else {
							nextRow.Add (ConsoleColor.White);
						}
					}
					nextRow.Add ($" :");
				}
				else {
					nextRow.Add ($"  ");
				}

				nextRow.Add ("  ");
			}

			res.Add ($"\n");
			res.AddRange (nextRow);
			res.Add ($"\n");
		}

		return res;
	}

	public void PrintSelf (WormyPivot focus = null)
	{
		lock (Console.Out) {
			var highlight = focus == null
				? Array.Empty<Worm> ()
				: focus.InvolvedWorms ().ToArray ();
			var ps = TextualRepresentationEx (highlight);
			var setBG = false;
			foreach (var p in ps) {
				if (p == Logging.ResetColor) {
					Console.BackgroundColor = ConsoleColor.Black;
				}
				else if (p == Logging.Background) {
					setBG = true;
				}
				else if (p is string s) {
					Console.Write (s);
				}
				else if (p is ConsoleColor c) {
					if (setBG) {
						setBG = false;
						Console.BackgroundColor = c;
					}
					else {
						Console.ForegroundColor = c;
					}
				}
			}
			Console.ResetColor ();
		}
	}

	public List<SmallSet<Worm>> Subspaces ()
	{
		using var worms = new SmallSet<Worm> (AllWorms);
		worms.RemoveWhere (w => w.Cells.Length >= 5);
		var sets = BuildConnectedComponents (
			this,
			worms,
			true // todo warum true?
			);
		return sets;
	}

	/// <summary>
	/// Get connected components.
	/// </summary>
	/// <param name="grid">
	/// Grid on which the string live.
	/// </param>
	/// <param name="openSet">
	/// The strings that are used in the components.
	/// Any strings NOT in this set will NOT be part of the results.
	/// </param>
	/// <returns>
	/// Connected components consisting (only) of the open strings.
	/// </returns>
	public static List<SmallSet<Worm>> BuildConnectedComponents (
		WormyBoardGrid grid,
		IReadOnlyCollection<Worm> openSet,
		bool followDisallowedNeighbors)
	{
		/*
		 * "open" is the set of all nodes that are to be assigned to connected groups.
		 */
		using var open = new SmallSet<Worm> (openSet);
		var nOpen = open.Count;

		/*
		 * While there is an unassigned node, pick it and create a new set for it
		 */
		var components = new List<SmallSet<Worm>> ();
		while (open.Count > 0) {
			var pivot = open.Pull ();
			var set = new SmallSet<Worm> (open.Count);
			set.Add (pivot);
			components.Add (set);

			/*
			 * The set of reachable nodes is concurrently used as a work queue.
			 * New items are added at the end.
			 * Items are never added again.
			 * Each added item is always a valid part of the result.
			 */
			var iter = set.GetMutableEnumerator ();
			while (iter.MoveNext ()) {
				var cur = iter.Current;
				foreach (var nb in cur._Neighbors) {
					if (!followDisallowedNeighbors && cur.IsNotAllowedToConnectTo (nb)) {
						continue;
					}
					/*
					 * If this node was not found before, grab it.
					 * Restricted nodes are not in the open set, so this is included in this check.
					 */
					if (open.Remove (nb)) {
						/*
						 * Add this node to the set of reachable nodes.
						 * This also means this node becomes a future work item and will be enumerated over.
						 * Note that each worm can appear here at most once (because it has been removed from the open list).
						 */
						set.Add (nb);
					}
				}
			}
		}

		Debug.Assert (components.Count <= nOpen);
		return components;
	}

	public SmallSet<Worm> TryInsertPiece (Piece piece, int x0, int y0)
	{
		var worms = new SmallSet<Worm> (5);
		var cellCount = 0;

		for (int i = 0; i < piece._Fill.Length; i++) {
			if (!piece._Fill [i]) {
				continue;
			}
			var x = x0 + (i % piece._SX);
			var y = y0 + (i / piece._SX);
			var cellIndex = x + SX * y;
			var add = WormInCell [cellIndex];

			// Check if a contained cell is closed
			if (add == null) {
				return null;
			}

			// Check if the contained worms are not allowed to connect
			foreach (var prev in worms) {
				if (prev.IsNotAllowedToConnectTo (add)) {
					return null;
				}
			}

			if (worms.Add (add)) {
				cellCount += add.Cells.Length;
				if (cellCount > 5) {
					// Worms stretch beyond the area of this piece
					return null;
				}
			}
		}

		Debug.Assert (cellCount == 5);
		return worms;
	}
}
