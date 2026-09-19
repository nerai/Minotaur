using System;
using System.Collections.Generic;
using Minotaur.Boards.Common;
using Minotaur.ExactCover;
using Minotaur.GenericTreeSearch;

namespace Minotaur.Boards.Dancing;

public sealed class DancingSolutionPivot : TreePivot<BoardGrid>
{
	public DancingSolutionPivot (
		IExpandMeta meta,
		double desire,
		LinkedMatrixTreeNode pivot)
		: base (meta, desire, pivot)
	{
	}

	protected override void CreateChildren ()
	{
		// We do this ourselves...
		throw new NotSupportedException ();
	}

	public override string ToString ()
	{
		return $"{base.ToString ()}  {Pivot}";
	}
}
