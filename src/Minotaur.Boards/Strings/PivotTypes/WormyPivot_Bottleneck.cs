using System.Collections.Immutable;
using System.Diagnostics;

namespace Minotaur.Boards.Strings;

public sealed class WormyPivot_Bottleneck : WormyPivot
{
	public WormyPivot_Bottleneck (
		WormyNode parent,
		WormyTreeCreationMeta meta,
		(Worm pivot, Worm cut, ImmutableArray<Worm> reachable, int total) bestRestriction,
		double desire)
		:
		base (meta, desire, bestRestriction, meta.Cost_Bottleneck)
	{
		var action = new WormyAction_Bottleneck (
			meta,
			this,
			parent,
			bestRestriction.pivot,
			bestRestriction.cut,
			bestRestriction.reachable);
		AddChild (action);
	}

	protected override string GetPivotInfo ()
	{
		var piv = ((Worm pivot, Worm cut, ImmutableArray<Worm> reachable, int total)) Pivot;
		return $"bottleneck in [{piv.cut.ToShortString ()}]";
	}

	public override IEnumerable<Worm> InvolvedWorms ()
	{
		var piv = ((Worm pivot, Worm cut, ImmutableArray<Worm> reachable, int total)) Pivot;
		yield return piv.cut;
		Debug.Assert (piv.reachable.Contains (piv.pivot));
		var act = (WormyAction_Bottleneck) ChildActions.Single ();
		yield return act.MergedWorm;
	}
}
