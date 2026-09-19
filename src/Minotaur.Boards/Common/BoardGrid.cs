using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Minotaur.Utils;

using static Minotaur.Utils.Logging;

namespace Minotaur.Boards.Common;

public class BoardGrid : ISolutionGrid
{
	public enum TextualRepresentationStyle
	{
		Condensed,
		Wide,
		Literals,
	}

	public readonly int SX;
	public readonly int SY;

	[IgnoreDataMember]
	public (int SX, int SY) Size => (SX, SY);

	private readonly char [] _Cells;

	public int OpenTileCount { get; private set; }

	private List<BoardGrid> _Rots = null;

	public int Variant {
		get;
		private set;
	} = -1;

	public int UnfinishedCells => OpenTileCount;

	public BoardGrid (int sx = 10, int sy = 6, string closed = null)
	{
		SX = sx;
		SY = sy;
		_Cells = new char [SX * SY];
		OpenTileCount = SX * SY;

		if (closed != null) {
			closed = closed.Replace (",", "");
			InitializeFromClosedString (closed);
		}
	}

	/// <summary>
	/// Create BoardGrid from BitArray
	/// </summary>
	/// <param name="closed">
	/// Each bit denotes a cell: 0 means open, 1 means closed
	/// </param>
	public BoardGrid (int sx, int sy, SmallBitArray closed)
	{
		SX = sx;
		SY = sy;
		_Cells = new char [SX * SY];
		OpenTileCount = SX * SY;

		if (closed.Length != SX * SY) {
			throw new ArgumentException ();
		}

		var n = _Cells.Length;
		for (int cellIndex = 0; cellIndex < n; cellIndex++) {
			char c = closed [n - cellIndex - 1] ? char.MaxValue : (char) 0;
			SetClosed (cellIndex, c);
		}
	}

	public BoardGrid (string boardHash)
	{
		var split = boardHash.Split (',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		SY = split.Length;
		SX = split.Select (s => s.Length).Distinct ().Single ();
		_Cells = new char [SX * SY];
		OpenTileCount = SX * SY;

		var closed = string.Join ("", split);
		InitializeFromClosedString (closed);
	}

	private void InitializeFromClosedString (string closed)
	{
		if (closed.Length != _Cells.Length) {
			throw new ArgumentException ("closed.Length != _Cells.Length");
		}

		for (int cellIndex = 0; cellIndex < SX * SY; cellIndex++) {
			var c = closed [cellIndex];
			switch (c) {
				case '0':
					// already initialized like this
					break;
				case '1':
					SetClosed (cellIndex, char.MaxValue);
					break;
				default:
					SetClosed (cellIndex, c);
					break;
			}
		}
	}

	public List<BoardGrid> ValidUniqueRotations ()
	{
		if (_Rots == null) {
			var list = new List<BoardGrid> ();
			var p = new Piece (-1, SX, SY, _Cells.Select (c => c != 0), null, true, -1);

			foreach (var rot in p._UniqueRotations) {
				var v = rot.Value;

				if (v._SX < v._SY) {
					// discard vertical rotations, as usual for boards.
					continue;
				}

				var b = new BoardGrid (v._SX, v._SY);
				for (int y = 0; y < b.SY; y++) {
					for (int x = 0; x < b.SX; x++) {
						var f = v._Fill [x + y * b.SX];
						var c = f ? char.MaxValue : (char) 0;
						b.SetClosed (x, y, c);
					}
				}

				list.Add (b);
			}

			_Rots = list.OrderBy (v => v.GetCellsAsString ()).ToList ();

			for (int i = 0; i < _Rots.Count; i++) {
				_Rots [i].Variant = i + 1;
			}
		}

		return _Rots;
	}

	/// <summary>
	/// Return a clone of this board onto which a piece was placed.
	/// </summary>
	public BoardGrid ClonePutPiece (IEnumerable<int> coveredCells, char pieceName)
	{
		var b = new BoardGrid (SX, SY);
		Array.Copy (_Cells, b._Cells, _Cells.Length);
		b.OpenTileCount = OpenTileCount;

		foreach (var cellIndex in coveredCells) {
			Debug.Assert (b._Cells [cellIndex] == 0);
			b.SetClosed (cellIndex, pieceName);
		}

		return b;
	}

	public ImmutableArray<byte> GetUniqueHash ()
	{
		var b = ImmutableArray.CreateBuilder<byte> (4 + 1 + SX * SY);

		b.Add (0);
		b.Add (0);
		b.Add (0);
		b.Add (0);

		Debug.Assert (SX <= 255);
		b.Add ((byte) SX);

		foreach (var c in _Cells) {
			Debug.Assert ((c == char.MaxValue) || (c <= 255));
			b.Add ((byte) c);
		}

		var hash = Hashing.SDBM (b);
		b [0] = (byte) (hash >> 24);
		b [1] = (byte) (hash >> 16);
		b [2] = (byte) (hash >> 8);
		b [3] = (byte) (hash >> 0);

		return b.ToImmutable ();
	}

	private string _InvariantName = null;

	/// <summary>
	/// Canonical / invariant name.
	/// Board arrangement of smallest cells first.
	/// 
	/// e.g. for a 4x2 board
	/// 0011
	/// 1110
	/// </summary>
	public string InvariantName {
		get {
			if (_InvariantName == null) {
				_InvariantName = ValidUniqueRotations ().First ().GetCellsAsString ();
			}
			return _InvariantName;
		}
	}

	public string NotInvariantName {
		get {
			return GetCellsAsString ();
		}
	}

	public ulong InvariantNameAsInt {
		get {
			var cells = ValidUniqueRotations ().First ()._Cells;
			if (cells.Length > 64) {
				throw new InvalidOperationException ("InvariantNameAsInt is only supported boards with up to 64 cells.");
			}
			ulong u = 0;
			foreach (char c in cells) {
				u <<= 1;
				if (c == 0) {
					u |= 0u;
				}
				else if (c == char.MaxValue) {
					u |= 1u;
				}
				else {
					throw new InvalidOperationException ("InvariantNameAsInt is only supported for empty boards.");
				}
			}
			return u;
		}
	}

	public string ExtendedName {
		get {
			return $"{PrefixForStorage}; {InvariantName}; {Variant}/{ValidUniqueRotations ().Count}";
		}
	}

	public string PrefixForStorage {
		get {
			var hash = Hashing.SDBM (InvariantName);
			var cs = new char [2];

			hash ^= hash >> 16;
			cs [0] = (char) ('A' + (hash % 26u));
			hash /= 26;
			cs [1] = (char) ('A' + (hash % 26u));
			//hash /= 26;

			return new string (cs);
		}
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public bool IsClosedAt (int x, int y)
	{
		return _Cells [y * SX + x] != 0;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public char CellAt (int x, int y)
	{
		return _Cells [y * SX + x];
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public void SetClosed (int x, int y, char closed)
	{
		SetClosed (y * SX + x, closed);
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public void SetClosed (int cellIndex, char closed)
	{
		var old = _Cells [cellIndex];
		_Cells [cellIndex] = closed;
		OpenTileCount += (old != 0) ? 1 : 0;
		OpenTileCount -= (closed != 0) ? 1 : 0;
	}

	public static IEnumerable<SmallBitArray> GenerateRandom (int sx, int sy, int pieces)
	{
		var r = new Random ();

		while (true) {
			var bits = new SmallBitArray (sx * sy);
			for (int i = 0; i < sx * sy; i++) {
				bits [i] = r.Next () % 2 == 0;
			}
			yield return bits;
		}
	}

	public List<List<int>> GetSubspaces ()
	{
		IEnumerable<int> Neighbours (int c)
		{
			var x = c % SX;
			var y = c / SX;

			if (x > 0) yield return c - 1;
			if (y > 0) yield return c - SX;
			if (x < SX - 1) yield return c + 1;
			if (y < SY - 1) yield return c + SX;
		}

		using var done = new SmallSet<int> (SX * SY);
		List<int> CreateSpace (int c)
		{
			var space = new List<int> ();
			var work = new Queue<int> ();
			work.Enqueue (c);

			while (work.Any ()) {
				var cur = work.Dequeue ();
				if (_Cells [cur] != 0) {
					continue;
				}
				if (!done.Add (cur)) {
					continue;
				}
				space.Add (cur);
				foreach (var n in Neighbours (cur)) {
					work.Enqueue (n);
				}
			}
			return space;
		}

		var spaces = new List<List<int>> ();
		for (int y = 0; y < SY; y++) {
			for (int x = 0; x < SX; x++) {
				var space = CreateSpace (y * SX + x);
				if (space.Any ()) {
					spaces.Add (space);
				}
			}
		}

		return spaces;
	}

	public bool IsPointSymmetric ()
	{
		for (int y = 0; y < (SY + 1) / 2; y++) {
			for (int x = 0; x < SX; x++) {
				var c1 = CellAt (x, y);
				var c2 = CellAt (SX - 1 - x, SY - 1 - y);
				if (c1 != c2) {
					return false;
				}
			}
		}
		return true;
	}

	/// <summary>
	/// Find subspace with cell count not divisible by 5.
	/// </summary>
	public ImmutableArray<int>? FindBadSubspace ()
	{
		var spaces = GetSubspaces ();
		var bad = spaces
			.Where (space => space.Count % 5 != 0)
			.OrderBy (space => space.Count)
			.FirstOrDefault ()
			?.ToImmutableArray ();
		return bad;
	}

	public static BigInteger NumberOfBoards (int sx, int sy, int pieces)
	{
		var n = sx * sy;
		var k = 5 * pieces;
		return Util.Fac3 (n - k, k);
	}

	public static IEnumerable<SmallBitArray> GenerateAll (int sx, int sy, int pieces, BigInteger skip)
	{
		var zeros = 5 * pieces;
		var ones = sx * sy - zeros;
		Debug.Assert (ones + zeros == sx * sy);
		foreach (var perm in BinaryPermutation.BinaryPermutationsIndexed (ones, zeros, skip)) {
			Debug.Assert (perm.Count == sx * sy);
			yield return perm;
		}
	}

	public void PrintSelf ()
	{
		Log (TextualRepresentation (TextualRepresentationStyle.Condensed));
	}

	public string TextualRepresentation (TextualRepresentationStyle style, List<int> highlight = null)
	{
		var sb = new StringBuilder ();
		for (int y = 0; y < SY; y++) {
			for (int x = 0; x < SX; x++) {
				int i = y * SX + x;
				var c = _Cells [i];
				var closed = IsClosedAt (x, y);
				switch (style) {
					case TextualRepresentationStyle.Condensed:
						sb.Append (closed ? "##" : "[]");
						break;
					case TextualRepresentationStyle.Wide:
						if (highlight?.Contains (i) == true) {
							sb.Append (closed ? " !! " : $"!{i:00}!");
						}
						else {
							sb.Append (closed ? " ## " : $"[{i:00}]");
						}
						break;
					case TextualRepresentationStyle.Literals:
						if (highlight?.Contains (i) == true) {
							sb.Append ("!! ");
						}
						else {
							if (c == 0) {
								sb.Append ($"{i:00} ");
							}
							else if (c == char.MaxValue) {
								sb.Append ($"## ");
							}
							else {
								sb.Append ($"{c}{c} ");
							}
						}
						break;
					default:
						throw new ArgumentException ();
				}
			}
			sb.AppendLine ();
		}
		return sb.ToString ();
	}

	public string GetCellsAsString ()
	{
		var sb = new StringBuilder ();
		for (int y = 0; y < SY; y++) {
			if (y > 0) {
				sb.Append (",");
			}
			for (int x = 0; x < SX; x++) {
				var c = _Cells [y * SX + x];
				switch (c) {
					case (char) 0:
						sb.Append ("0");
						break;
					case char.MaxValue:
						sb.Append ("1");
						break;
					default:
						sb.Append (c);
						break;
				}
			}
		}
		return sb.ToString ();
	}
}
