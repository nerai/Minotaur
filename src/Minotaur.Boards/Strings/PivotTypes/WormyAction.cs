using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using Minotaur.Boards.Common;
using Minotaur.GenericTreeSearch;

namespace Minotaur.Boards.Strings;

public abstract class WormyAction : TreeAction<WormyBoardGrid>
{
	protected WormyTreeCreationMeta Meta => (WormyTreeCreationMeta) _Meta;

	public readonly Worm Pivot;

	public WormyAction (WormyTreeCreationMeta meta, Worm pivot)
		: base (meta)
	{
		Pivot = pivot ?? throw new ArgumentNullException (nameof (pivot));
	}

	/// <summary>
	/// Find the oldest ancestor of this worm.
	/// If there are multiple parents, deterministically pick any of them.
	/// </summary>
	public abstract Worm ParentOfWorm (Worm child);

	public abstract IEnumerable<Worm> HotWorms ();
}
