using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Minotaur.ExactCover;

// Column header in 2d matrix
public class ColHead : QLCell
{
	internal int RowCount = 0;

	public bool IsOptional = false;

	internal protected ColHead ()
	{
		Head = this;
		RowIndex = 0;
	}

	public override string ToString ()
	{
		return $"ColHead-{GetHashCode ()}";
	}

	internal void AddHead (ColHead head)
	{
		head.L = L;
		head.R = this;
		L.R = head;
		L = head;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	internal QLCell CreateRowNode (Stack<QLCell> pool, uint rowIndex)
	{
		var node = QLCell.GetFromPool (pool);
		node.Head = this;
		node.RowIndex = rowIndex;

		// todo: die node wird "oben" statt "unten" eingefügt. das ist funktional komplett identisch aber etwas anders als erwartet. bitte mal anpassen.
		node.D = this;
		node.U = U;
		U.D = node;
		U = node;
		RowCount += 1;
		return node;
	}

	/// <summary>
	/// Detach this row.
	/// Also detach all rows linked to it, since none of them can be used anymore when this column is blocked.
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	internal void Detach ()
	{
		R.L = L;
		L.R = R;

		var row = D;
		while (row != this) {
			var col = row.R;
			while (col != row) {
				col.D.U = col.U;
				col.U.D = col.D;
				col.Head.RowCount -= 1;
				col = col.R;
			}
			row = row.D;
		}
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	internal void Reattach ()
	{
		var row = U;
		while (row != this) {
			var col = row.L;
			while (col != row) {
				col.Head.RowCount += 1;
				col.D.U = col;
				col.U.D = col;
				col = col.L;
			}
			row = row.U;
		}

		R.L = this;
		L.R = this;
	}
}
