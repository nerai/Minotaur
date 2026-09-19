using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Minotaur.Boards.Common;
using Minotaur.GenericTreeSearch;
using static Minotaur.Boards.Common.BoardGrid;

namespace Minotaur.Boards.Common;

public abstract class TreeNode<TGrid>
	: AbstractTreeNode
	where TGrid : ISolutionGrid
{
	protected override char _DebugPrefix () => 'N';

	public readonly TGrid Grid;

	public IEnumerable<TreeAction<TGrid>> Parents => TreeParents.Cast<TreeAction<TGrid>> ();

	public IEnumerable<TreePivot<TGrid>> Pivots => AllTreeChildren.Cast<TreePivot<TGrid>> ();

	public readonly List<UnsolvabilityInfo> Info = new ();

	public bool SolvabilityIsKnown => IsSolvable.HasValue;

	public BigInteger PrunedNodeCount { get; set; } = 0;

	public int UsageCounter = 0;

	public TreeNode (IExpandMeta meta, TGrid grid)
		: base (meta, false)
	{
		Grid = grid;
	}

	public virtual string TextualRepresentation (TextualRepresentationStyle style, List<int> highlight = null)
	{
		return Grid.TextualRepresentation (style, highlight);
	}

	public IEnumerable<TreeNode<TGrid>> EnumerateSubtreeDistinct ()
	{
		var stack = new Stack<TreeNode<TGrid>> ();
		stack.Push (this);

		var done = new HashSet<TreeNode<TGrid>> ();

		while (stack.Count > 0) {
			var node = stack.Pop ();
			if (!done.Add (node)) {
				continue;
			}
			yield return node;
			foreach (var piv in node.Pivots) {
				foreach (var act in piv.ChildActions) {
					stack.Push (act.Child);
				}
			}
		}
	}

	public override void PruneChild (AbstractTreeNode child)
	{
		//Console.WriteLine ($"PruneChild {child._DebugName}");
		base.PruneChild (child);

		var piv = (TreePivot<TGrid>) child;
		foreach (var act in piv.ChildActions.ToArray ()) {
			piv.PruneChild (act);
		}
	}

	public string GetUnsolvabilityInfoAsMultiline ()
	{
		if (Info.Count == 0) {
			throw new Exception ("should not happen");
		}
		else if (Info.Count == 1) {
			return $"{Info [0].MainReason}\n{Info [0].Detail}";
		}
		else {
			return string.Join ("\n", Info.Select (info => $"{info.MainReason} {info.Detail};"));
		}
	}

	public string GetUnsolvabilityInfoAsMultilineHtml ()
	{
		if (Info.Count == 0) {
			throw new Exception ("should not happen");
		}
		else if (Info.Count == 1) {
			return $"{Info [0].MainReason}";
		}
		else {
			return string.Join ("\n", Info.Select (info => $"{info.MainReason}"));
		}
	}
}
