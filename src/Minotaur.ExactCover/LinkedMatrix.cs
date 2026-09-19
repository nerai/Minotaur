using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Minotaur.ExactCover;

/// <summary>
/// 2d doubly linked grid of cells, a matrix
/// 
/// Can solve Exact Set Cover problems.
/// 
/// Can generate a log of the solving process, to do so subscribe to Enter/Leave events.
/// </summary>
public class LinkedMatrix
{
	public readonly bool ExploreOnlyOneColumn;

	private readonly Stack<QLCell> _Pool;
	private readonly ColHead _Root;

	private readonly Stack<QLCell> _CurrentPath = new Stack<QLCell> ();
	public IReadOnlyCollection<QLCell> CurrentPath => _CurrentPath;

	/// <summary>
	/// Use this to get a thread-safe row IDs for this matrix
	/// </summary>
	private uint _RowCounter;

	/// <summary>
	/// Each solution is an ordered list of placements.
	/// Each placement is a complete list of row elements that can be used to identify the placement.
	/// 
	/// Note that the solution nodes are ordered according to how easy they were to create, i.e. fewest options available
	/// </summary>
	public Func<Stack<QLCell>, bool> SolutionFound;

	public delegate void EnterColumnEvent (ColHead column, int worseThanBest);
	/// <summary>
	/// Regularly, it suffices to enter one single column to find ALL(!) solutions.
	/// 
	/// If more than one columns is entered, the algorithm is changed to explore different pivots to arrive at these (all) solutions.
	/// Different pivots may be faster or (usually) slower.
	/// </summary>		
	public EnterColumnEvent EnterColumn;
	public Action<ColHead> LeaveColumn;

	/// <summary>
	/// Rows represent different ways to fulfill a requirement.
	/// An arbitrary number of them (between 0 and all) leads to solutions.
	/// 
	/// Enumeration over rows is required for backtracking.
	/// If ALL rows are enumerated, regardless if a solution was found already, then all solutions will be found.
	/// </summary>
	public delegate void EnterRowEvent (QLCell verticalPivot, out bool allow);
	public EnterRowEvent EnterRow;
	public Action<QLCell> LeaveRow;

	public LinkedMatrix (Stack<QLCell> pool, bool exploreOnlyOneColumn)
	{
		_Pool = pool;
		ExploreOnlyOneColumn = exploreOnlyOneColumn;

		_Root = new ColHead ();
	}

	public void AddColumn (ColHead head)
	{
		_Root.AddHead (head);
	}

	public QLCell InsertRow (ColHead primary, ColHead [] connected)
	{
		var rowIndex = _RowCounter++;
		var row = primary.CreateRowNode (_Pool, rowIndex);

		var pivot = row;
		foreach (var col in connected) {
			var add = col.CreateRowNode (_Pool, rowIndex);
			pivot.R = add;
			add.L = pivot;
			pivot = add;
		}
		pivot.R = row;
		row.L = pivot;

		return row;
	}

	public void Free ()
	{
		var col = _Root.R;
		while (col != _Root) {
			var row = col.D;
			while (row != col) {
				Debug.Assert (!(row is ColHead));
				QLCell.ReturnToPool (row, _Pool);
				row = row.D;
			}
			col = col.R;
		}
	}

	//[DebuggerStepThrough]
	public void Solve ()
	{
		if (HasOnlyOptionalColumns ()) {
			var shouldContinue = SolutionFound?.Invoke (_CurrentPath);
			return;
		}

		void SolveForColumn (ColHead col, int worse)
		{
			EnterColumn?.Invoke (col, worse);

			/*
			 * As the pivot column is, by definition, solved, we can remove it immediately
			 */
			col.Detach ();

			/*
			 * The vertical nodes in the column are the options that we try.
			 * We don't know which ones will work.
			 */
			var vertical = col.D;
			while (vertical != col) {
				Debug.Assert (vertical.Head == col);

				/*
				 * This option covers some columns, so we remove these.
				 */
				var horizontal = vertical.R;
				while (horizontal != vertical) {
					horizontal.Head.Detach ();
					horizontal = horizontal.R;
				}

				/*
				 * Recurse
				 */
				_CurrentPath.Push (vertical);
				bool allowed = true;
				EnterRow?.Invoke (vertical, out allowed);
				if (allowed) {
					Solve ();
					LeaveRow?.Invoke (vertical);
				}
				_CurrentPath.Pop ();

				/*
				 * Restore everything
				 */
				horizontal = vertical.L;
				while (horizontal != vertical) {
					horizontal.Head.Reattach ();
					horizontal = horizontal.L;
				}

				/*
				 * Go to next option (= row)
				 */
				vertical = vertical.D;
			}

			/*
			 * Restore the pivot column
			 */
			col.Reattach ();

			LeaveColumn?.Invoke (col);
		}

		if (ExploreOnlyOneColumn) {
			// Regular algorithm: consider only a single shortest column (there may be many, but only one is chosen)
			var shortest = FindShortestColumn ();
			SolveForColumn (shortest, 0);
		}
		else {
			// For full tree, consider all possible short enough columns
			int maxWorse = 1;
			var cols = FindShortestColumns (maxWorse)
				.OrderByDescending (pair => pair.worse)
				.ToList ();

			foreach (var pair in cols) {
				SolveForColumn (pair.col, pair.worse);
			}
		}
	}

	private bool HasOnlyOptionalColumns ()
	{
		var col = (ColHead) _Root.R;
		while (col != _Root) {
			if (!col.IsOptional) {
				return false;
			}
			col = (ColHead) col.R;
		}
		return true;
	}

	private ColHead FindShortestColumn ()
	{
		ColHead min_col = null;
		int min_n = int.MaxValue;
		var col = (ColHead) _Root.R;
		while (col != _Root) {
			if ((col.RowCount < min_n) && (!col.IsOptional)) {
				min_n = col.RowCount;
				min_col = col;
			}
			col = (ColHead) col.R;
		}
		return min_col;
	}

	private IEnumerable<(ColHead col, int worse)> FindShortestColumns (int maxWorse)
	{
		// If there is only 1 possibility in the shortest column, always use it
		// TODO das nicht unbedingt immer tun - es koennte eine valide permutation geben die schlechteres pivot hat - ist vllt interessant
		int min_n = FindShortestColumn ().RowCount;
		if (min_n <= 1) {
			maxWorse = 0;
		}

		var col = (ColHead) _Root.R;
		while (col != _Root) {
			int worse = col.RowCount - min_n;
			if ((worse <= maxWorse) && (!col.IsOptional)) {
				yield return (col, worse);
			}
			col = (ColHead) col.R;
		}
	}

	public string RowsToString ()
	{
		var sb = new StringBuilder ();

		{
			QLCell col = _Root;
			do {
				col = col.R;
				sb.Append (col.Head);
				sb.Append (" ");
			} while (col != _Root);
			sb.Append ("\n");

			col = _Root;
			do {
				col = col.R;
				sb.Append (col.Head.RowCount);
				sb.Append (" ");
			} while (col != _Root);
			sb.Append ("\n");
		}

		QLCell pivot = _Root;
		for (int i = 0; i < 12; i++) {
			pivot = pivot.L;

			var row = pivot.D;
			while (row != pivot) {
				QLCell col = _Root;
				do {
					col = col.R;

					bool contains = false;
					var horiz = row;
					do {
						contains |= col == horiz.Head;
						horiz = horiz.R;
					} while (horiz != row);

					if (contains) {
						sb.Append (col.Head);
					}
					else {
						sb.Append (".");
						for (int ii = 1; ii < col.Head.ToString ().Length; ii++) {
							sb.Append (" ");
						}
					}
					sb.Append (" ");
				} while (col != _Root);

				sb.Append ("\n");
				row = row.D;
			};
		}

		return sb.ToString ();
	}
}
