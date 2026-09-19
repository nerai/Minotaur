using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace Minotaur.Boards.Strings;

public static class WormExtensions
{
	/// <summary>
	/// Get a value that is compatible with the FillHash of a Piece.
	/// 
	/// As string based pieces do not store fill status, but only member
	/// cells indexes, some conversion is required.
	/// </summary>
	public static uint GetFillHashOfPiece (this IEnumerable<int> cells, int gridSX, int gridSY)
	{
		/*
		 * Translate piece to origin.
		 */
		var x0 = int.MaxValue;
		var x1 = int.MinValue;
		var y0 = int.MaxValue;
		foreach (var c in cells) {
			var x = c % gridSX;
			var y = c / gridSX;
			x0 = Math.Min (x0, x);
			x1 = Math.Max (x1, x);
			y0 = Math.Min (y0, y);
		}
		var pieceSX = x1 - x0 + 1;

		uint hash = (uint) pieceSX;
		hash <<= 5 * 5;
		foreach (var c in cells) {
			/*
			 * Find coord of this cell relative to the piece origin
			 */
			var x = c % gridSX - x0;
			var y = c / gridSX - y0;
			Debug.Assert (x >= 0 && y >= 0);
			Debug.Assert (x < 5 && y < 5);

			/*
			 * Set the bit for this cell.
			 * Cells not part of this piece do not change the hash, so they can be ignored.
			 */
			var shift = x + pieceSX * y;
			hash |= 1u << shift;
		}
		return hash;
	}
}
