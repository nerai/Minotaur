using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using Minotaur.Boards.Common;
using Minotaur.GenericTreeSearch;
using Minotaur.Utils;

namespace Minotaur.Boards.Strings;

public abstract class WormyPivot : TreePivot<WormyBoardGrid>
{
	protected WormyTreeCreationMeta Meta => (WormyTreeCreationMeta) _Meta;

	public WormyPivot (
		WormyTreeCreationMeta meta,
		double desire,
		object pivot,
		SaturatingCost localCost)
		: base (meta, desire, pivot)
	{
		LocalCost = localCost;
	}

	protected override void CreateChildren ()
	{
		/*
		 * Children are already added in ctors in all derived classes.
		 * We do not need to do anything here.
		 */
	}

	public abstract IEnumerable<Worm> InvolvedWorms ();
}
