using System;
using System.Collections.Generic;
using System.Diagnostics;
using Minotaur.Boards.Common;
using Minotaur.ExactCover;

namespace Minotaur.Boards.Dancing;

public sealed class PentoColHead : ColHead
{
	public readonly string PieceId;
	public readonly int CellCoord;

	public PentoColHead (Piece piece)
	{
		Debug.Assert (piece.Name.Length == 1);

		PieceId = piece.Name;
		CellCoord = -1;
	}

	public PentoColHead (int cellCoord)
	{
		PieceId = null;
		CellCoord = cellCoord;
	}

	public override string ToString ()
	{
		return CellCoord >= 0
			? $"{CellCoord}"
			: PieceId;
	}
}
