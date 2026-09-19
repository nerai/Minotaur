using System;
using System.Collections.Generic;
using Minotaur.Boards.Common;
using Minotaur.GenericTreeSearch;

namespace Minotaur.Boards.Dancing;

public sealed class DancingSolutionNode : TreeNode<BoardGrid>
{
	public DancingSolutionNode (
		IExpandMeta meta,
		BoardGrid grid)
		: base (meta, grid)
	{
		if (grid.UnfinishedCells == 0) {
			SetIsSolution (true);
		}
	}

	protected override void CreateChildren ()
	{
		// We do this ourselves...
		throw new NotSupportedException ();
	}

	internal void AddPivot (DancingSolutionPivot piv)
	{
		AddTreeChild (piv);
	}

	public override string ToString ()
	{
		return "";
	}

	/* TODO
	public override void Freeze ()
	{
		base.Freeze ();

		if (!IsSolvable) {
			var badSS = Grid.FindBadSubspace ();
			if (badSS != null) {
				var s = string.Join (",", badSS);
				Info.Add (new UnsolvabilityInfo (
					UnsolvabilityInfo.UnsolvabilityReason.SSD,
					s,
					$"Subspace not divisible by 5: {s}"));
			}

			if (ExpandedChildGroups.Count == 0) {
				Info.Add (new UnsolvabilityInfo (
					UnsolvabilityInfo.UnsolvabilityReason.CannotKeepDancing,
					"No continuation.",
					"No continuation."));
			}
		}
	}
	*/
}
