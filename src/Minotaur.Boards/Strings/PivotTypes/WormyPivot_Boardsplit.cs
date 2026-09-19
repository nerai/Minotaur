using System.Collections.Immutable;

namespace Minotaur.Boards.Strings;

public sealed class WormyPivot_Boardsplit : WormyPivot
{
	public WormyPivot_Boardsplit (
		WormyNode parent,
		WormyTreeCreationMeta meta,
		(Worm restrict, ImmutableArray<Worm> set) tup,
		double desire)
		:
		base (meta, desire, tup.restrict, meta.Cost_Boardsplit)
	{
		var action = new WormyAction_Boardsplit (
			meta,
			this,
			parent,
			tup.restrict,
			tup.restrict,
			tup.set);
		AddChild (action);
	}

	protected override string GetPivotInfo ()
	{
		var piv = (Worm) Pivot;
		return $"board subdivision at [{piv.ToShortString ()}]";
	}

	public override IEnumerable<Worm> InvolvedWorms ()
	{
		yield return (Worm) Pivot;
	}
}
