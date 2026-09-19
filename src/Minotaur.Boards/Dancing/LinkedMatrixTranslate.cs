using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Minotaur.Boards.Common;
using Minotaur.ExactCover;
using Minotaur.GenericTreeSearch;
using Minotaur.Utils;

namespace Minotaur.Boards.Dancing;

public class LinkedMatrixTranslate
{
	public readonly BoardGrid Grid;
	public readonly LinkedMatrix M;
	public readonly LinkedMatrixTreeCreator TC;

	// Lookup maps for column heads
	private readonly Dictionary<string, PentoColHead> _ColHeadForPiece;
	private readonly Dictionary<int, PentoColHead> _ColHeadForCell;

	/// <summary>
	/// Lookup table for the association between a row in the matrix and a piece with a rotation.
	/// This is easier than checking which column combination refers to a piece/rotation.
	/// </summary>
	private readonly Dictionary<QLCell, Placement> _Row2Rotation;

	public int Steps => TC.Steps;
	public int StepsToFirst => TC.StepsToFirst;

	private static readonly ConcurrentDictionary<(Piece p, byte x, byte y, byte rot), Placement> _PlacementPool = new ();

	/// <summary>
	/// 
	/// </summary>
	/// <param name="grid"></param>
	/// <param name="_MovablePieces"></param>
	/// <param name="pool"></param>
	/// <param name="exploreOnlyOneColumn"></param>
	/// <param name="randomize"></param>
	/// <param name="possibleInsertLocationsHistogram">
	/// null, or array of size 12.
	/// If set, do NOT solve, but store how many rows are created for each piece.
	/// </param>
	public LinkedMatrixTranslate (
		BoardGrid grid,
		IReadOnlyList<Piece> _MovablePieces,
		Stack<QLCell> pool,
		bool exploreOnlyOneColumn,
		bool randomize,
		ulong [] possibleInsertLocationsHistogram,
		bool createTree)
	{
		M = new LinkedMatrix (pool, exploreOnlyOneColumn);

		// If only 1 column, there is no need to check for repetitions, they are impossible
		TC = new LinkedMatrixTreeCreator (
			M,
			checkRepetitions: !exploreOnlyOneColumn,
			createTree: createTree);

		Grid = grid;

		// Iterate over pieces, rotations, start X/Y coords
		var cellIndexes = new List<int> (grid.SX * grid.SY);
		for (int y = 0; y < grid.SY; y++) {
			for (int x = 0; x < grid.SX; x++) {
				if (grid.IsClosedAt (x, y)) {
					if (grid.CellAt (x, y) < char.MaxValue) {
						throw new NotImplementedException ("Translating existing pieces on a grid was not implemented in the solver");
					}
					continue;
				}
				var cellIndex = x + grid.SX * y;
				cellIndexes.Add (cellIndex);
			}
		}

		if (randomize) {
			cellIndexes.Shuffle ();
		}

		_ColHeadForCell = new Dictionary<int, PentoColHead> ();
		foreach (var cellIndex in cellIndexes) {
			var h = new PentoColHead (cellCoord: cellIndex);
			M.AddColumn (h);
			_ColHeadForCell [cellIndex] = h;
		}
		//MeasurePerformance ("_build_matrix: build grid")

		_ColHeadForPiece = new Dictionary<string, PentoColHead> ();
		var rowTups = new List<(Piece p, byte x, byte y, byte rot)> (Piece.PieceHashMap.Count * grid.SX * grid.SY);
		foreach (var p in _MovablePieces) {
			var h = new PentoColHead (piece: p);
			M.AddColumn (h); // Does not need to be randomized because it is an optional col
			_ColHeadForPiece [p.Name] = h;
			h.IsOptional = true;

			var rots = p._UniqueRotations;
			foreach (var rot in rots) {
				var x1 = grid.SX - rot.Value._SX + 1;
				var y1 = grid.SY - rot.Value._SY + 1;
				for (int y = 0; y < y1; y++) {
					for (int x = 0; x < x1; x++) {
						var key = (rot.Value, (byte) x, (byte) y, (byte) rot.Key);
						rowTups.Add (key);
					}
				}
			}
		}

		if (randomize) {
			rowTups.Shuffle ();
		}

		_Row2Rotation = new Dictionary<QLCell, Placement> ();
		foreach (var key in rowTups) {
			var (p, x, y, rot) = key;
			var row = TryInsertRow (p, x, y);
			if (row != null) {
				var placement = _PlacementPool.GetOrAdd (key, tup => new Placement (tup.p, tup.x, tup.y, tup.rot));
				_Row2Rotation [row] = placement;
				if (possibleInsertLocationsHistogram != null) {
					++possibleInsertLocationsHistogram [p.IntId];
				}
			}
			//Console.WriteLine ($"Piece {p} using {rots.Count} rotations was inserted in {nRows} rows.");
		}

		//Console.WriteLine ($"Matrix uses {_head_of_cell.Count} cells, {_head_of_piece.Count} pieces");
		if (_ColHeadForCell.Count != 5 * _ColHeadForPiece.Count) {
			//Console.WriteLine ("WARNING: number of 5-tile pieces and number of unoccupied cells do not correspond.");
		}

		if (possibleInsertLocationsHistogram == null) {
			M.Solve ();
			//MeasurePerformance ("solve");
		}

		M.Free ();
	}

	// Performance optimization: Keep a small buffer. Note: TryInsertRow must not ever be called in parallel.
	private readonly PentoColHead [] _TryInsertRowBuffer = new PentoColHead [5];

	private QLCell TryInsertRow (Piece piece, int x0, int y0)
	{
		var cell_cols = _TryInsertRowBuffer;
		var iCol = 0;
		for (int i = 0; i < piece._Fill.Length; i++) {
			if (!piece._Fill [i]) {
				continue;
			}
			var x = x0 + (i % piece._SX);
			var y = y0 + (i / piece._SX);
			var cellIndex = x + Grid.SX * y;

			if (!_ColHeadForCell.TryGetValue (cellIndex, out var col)) {
				/*
				 * Illegal board configuration. Either:
				 * a) grid cell occupied by tile
				 * b) opaque tile of piece covers transparent grid cell
				 */
				return null;
			}
			Debug.Assert (col.CellCoord == cellIndex);
			cell_cols [iCol++] = col;
		}

		Debug.Assert (_ColHeadForPiece [piece.Name].PieceId == piece.Name);

		var primary = _ColHeadForPiece [piece.Name];
		var inserted = M.InsertRow (primary, cell_cols);
		return inserted;
	}

	public List<List<Placement>> GetSolutions ()
	{
		var list = new List<List<Placement>> ();
		foreach (var solution in TC.Solutions) {
			var placements = new List<Placement> ();
			foreach (var step in solution) {
				foreach (var rownode in step) {
					if (_Row2Rotation.TryGetValue (rownode, out var placement)) {
						placements.Add (placement);
						//Console.WriteLine (placement);
						break;
					}
				}
			}
			list.Add (placements);
		}
		return list;
	}

	private DateTime _RecentPerformanceTime = DateTime.UtcNow;

	private void MeasurePerformance (string finished_section)
	{
		if (finished_section != null) {
			var dt = DateTime.UtcNow - _RecentPerformanceTime;
			Console.WriteLine ($"{dt.TotalMilliseconds:0.0}ms: {finished_section}");
		}
		_RecentPerformanceTime = DateTime.UtcNow;
	}

	public DancingSolutionNode TranslateEntireSolutionTree ()
	{
		static void CreateChildren (
			LinkedMatrixTreeNode parentRowNode,
			DancingSolutionNode solutionNode)
		{
			// Rows should, by definition, not be worse than anything - at least in classic DLX, which is what we use here
			Debug.Assert (parentRowNode.WorseThanBest == 0);
			Debug.Assert (parentRowNode.Pivot == null);

			foreach (var colNode in parentRowNode.Children) {
				if (!colNode.Children.Any ()) {
					// Do not create empty pivots
					continue;
				}

				var pivot = (PentoColHead) colNode.Pivot;
				var desire = 1.0 / (colNode.WorseThanBest + 1);
				var piv = new DancingSolutionPivot (solutionNode._Meta, desire, colNode);
				solutionNode.AddPivot (piv);

				foreach (var childRowNode in colNode.Children) {
					var usedColumns =
						from horiz in childRowNode.Option.EnumerateRow ()
						select (PentoColHead) horiz.Head;
					var coveredCells = usedColumns
						.Select (c => c.CellCoord)
						.Where (c => c >= 0)
						.OrderBy (i => i)
						.ToArray ();
					var pieceName = usedColumns
						.Select (c => c.PieceId)
						.Single (s => s != null);
					var childGrid = solutionNode.Grid.ClonePutPiece (coveredCells, pieceName [0]);
					var dchild = new DancingSolutionNode (piv._Meta, childGrid);

					var a = new DancingSolutionAction (piv._Meta, piv, dchild, coveredCells, pieceName);
					a.AddPivot (pivot, colNode.WorseThanBest); // todo das sollte in dancingPivot rein statt action, oder?

					CreateChildren (childRowNode, dchild);
				}

				piv.AfterExpansionComplete ();
			}

			solutionNode.AfterExpansionComplete ();
		}

		var mroot = TC.Root;
		var troot = new DancingSolutionNode (new FixedExpandMeta (), Grid);
		CreateChildren (mroot, troot);

		return troot;
	}
}
