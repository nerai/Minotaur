using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using static Minotaur.Utils.Logging;

namespace Minotaur.Boards.Common;

public class Piece
{
	public static readonly Piece [] AllPieces = new Piece [12];
	public static readonly Dictionary<uint, Piece> PieceHashMap = new Dictionary<uint, Piece> ();

	static Piece ()
	{
		int pieceId = 0;

		void CreateAndAddPiece (int sx, int sy, string name, string fill)
		{
			var split = fill
				.Split (',')
				.Select (s => s.Trim ())
				.Select (int.Parse)
				.Select (i => i != 0)
				.ToArray ();

			var p = new Piece (pieceId, sx, sy, split, name, true, -1);
			AllPieces [pieceId] = p;
			++pieceId;

			var rots = p._UniqueRotations;
			foreach (var rot in rots) {
				var hash = rot.Value.FillHash;
				PieceHashMap.Add (hash, rot.Value);
			}
		}

		CreateAndAddPiece (3, 3, "F", "0,1,1, 1,1,0, 0,1,0");
		CreateAndAddPiece (1, 5, "I", "1,1,1,1,1");
		CreateAndAddPiece (2, 4, "L", "1,0, 1,0, 1,0, 1,1");
		CreateAndAddPiece (2, 4, "N", "0,1, 0,1, 1,1, 1,0");
		CreateAndAddPiece (2, 3, "P", "1,1, 1,1, 1,0");
		CreateAndAddPiece (3, 3, "T", "1,1,1, 0,1,0, 0,1,0");
		CreateAndAddPiece (3, 2, "U", "1,0,1, 1,1,1");
		CreateAndAddPiece (3, 3, "V", "1,0,0, 1,0,0, 1,1,1");
		CreateAndAddPiece (3, 3, "W", "1,0,0, 1,1,0, 0,1,1");
		CreateAndAddPiece (3, 3, "X", "0,1,0, 1,1,1, 0,1,0");
		CreateAndAddPiece (4, 2, "Y", "0,0,1,0, 1,1,1,1");
		CreateAndAddPiece (3, 3, "Z", "1,1,0, 0,1,0, 0,1,1");
		LogN ($"Initialized piece hash map with {PieceHashMap.Count} items.");
	}

	// width in cells
	public readonly int _SX;

	// height in cells
	public readonly int _SY;

	// transparency
	// Note: The maximum size of this is 9, achieved by 3x3 pieces like V or W
	public readonly bool [] _Fill;

	// Cache of unique rotations
	public readonly Dictionary<int, Piece> _UniqueRotations;

	public readonly string Name;

	public readonly int IntId = -1; // todo use instead of string name?

	public readonly int RotationIndex;

	/// <summary>
	/// A hash of the cells occupied by this piece in this rotation.
	/// This is NOT a bitfield.
	/// </summary>
	public readonly UInt32 FillHash;

	public Piece (int id, int sx, int sy, IEnumerable<bool> fill, string name, bool generateRotations, int rotationIndex)
	{
		IntId = id;
		_SX = sx;
		_SY = sy;
		_Fill = fill.ToArray ();
		Name = name;
		RotationIndex = rotationIndex;

		FillHash = (uint) _SX;
		FillHash <<= 5 * 5;
		for (int i = _Fill.Length - 1; i >= 0; i--) {
			var set = _Fill [i] ? 1u : 0u;
			FillHash |= (set << i);
		}

		if (generateRotations) {
			_UniqueRotations = GenerateUniqueRotations ();
		}
	}

	public override string ToString ()
	{
		return $"{IntId}/{Name}";
	}

	// rotations with a different layout; including the base variant (0)
	private Dictionary<int, Piece> GenerateUniqueRotations ()
	{
		//Console.WriteLine ($"initialize rotations for {this}");
		var rots = new Dictionary<int, Piece> ();
		for (int roti = 0; roti < 8; roti++) {
			var rot = Rotate (roti);
			var found_equivalent = false;
			foreach (var existing in rots.Values) {
				var is_different = false;
				is_different |= rot._SX != existing._SX;
				is_different |= rot._SY != existing._SY;
				is_different |= !existing._Fill.SequenceEqual (rot._Fill);
				if (!is_different) {
					found_equivalent = true;
					break;
				}
			}
			if (!found_equivalent) {
				rots.Add (roti, rot);
			}
		}

		//Console.WriteLine ($"{this} has {rots .Length} unique rotations.");
		return rots;
	}

	private Piece Rotate (int roti)
	{
		bool mirror_h = (roti & 1) != 0;
		bool mirror_v = (roti & 2) != 0;
		bool flip_diag = (roti & 4) != 0;

		var fillp = new bool [_Fill.Length];
		for (int x = 0; x < _SX; x++) {
			for (int y = 0; y < _SY; y++) {
				var src = x + _SX * y;

				int xp = x;
				int yp = y;
				if (mirror_h) {
					xp = _SX - xp - 1;
				}
				if (mirror_v) {
					yp = _SY - yp - 1;
				}
				var dst = flip_diag
					? yp + _SY * xp
					: xp + _SX * yp;
				fillp [dst] = _Fill [src];
			}
		}
		var sx = _SX;
		var sy = _SY;
		if (flip_diag) {
			sx = _SY;
			sy = _SX;
		}

		var p = new Piece (IntId, sx, sy, fillp, Name, false, roti);
		return p;
	}
}
