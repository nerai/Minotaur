using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Minotaur.ExactCover;

/// <summary>
/// A step in the solving process of a LinkedMatrix.
/// </summary>
public class LinkedMatrixTreeNode
{
	public readonly LinkedMatrixTreeNode Parent;
	public readonly List<LinkedMatrixTreeNode> Children = new List<LinkedMatrixTreeNode> ();
	public readonly ColHead Pivot;
	public readonly QLCell Option;

	/// <summary>
	/// If this step would not normally be picked, store how much worse it is than the best option.
	/// </summary>
	public readonly int WorseThanBest;

	public override string ToString ()
	{
		return $"Piv={Pivot}, Opt={Option}, #Ch={Children.Count}";
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public LinkedMatrixTreeNode ()
	{
		Parent = null;
		Pivot = null;
		Option = null;
		WorseThanBest = 0;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public LinkedMatrixTreeNode (LinkedMatrixTreeNode parent, ColHead pivot, int worseThanBest)
	{
		Parent = parent;
		Pivot = pivot;
		Option = null;
		WorseThanBest = worseThanBest;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public LinkedMatrixTreeNode (LinkedMatrixTreeNode parent, QLCell option)
	{
		Parent = parent;
		Pivot = null;
		Option = option;
		WorseThanBest = 0;
	}

	public IEnumerable<uint> GetAllUsedRows ()
	{
		// TODO das ggf optimieren/cachen
		var node = this;
		do {
			// Is this a row node (not a column node)?
			if (node.Option != null) {
				yield return node.Option.RowIndex;
			}
			node = node.Parent;
		} while (node != null);
	}
}
