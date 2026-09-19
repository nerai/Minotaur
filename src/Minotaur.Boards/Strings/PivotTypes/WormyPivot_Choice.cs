using Minotaur.Utils;

namespace Minotaur.Boards.Strings;

public sealed class WormyPivot_Choice : WormyPivot
{
	public WormyPivot_Choice (
		WormyNode parent,
		WormyTreeCreationMeta meta,
		Worm pivot,
		SmallSet<Worm> nbs,
		double desire)
		:
		base (meta, desire, pivot, meta.Cost_Choice)
	{
		foreach (var nb in nbs) {
			var action = new WormyAction_Choice (
				meta,
				this,
				parent,
				pivot,
				nb,
				nbs);
			AddChild (action);
		}
	}

	protected override string GetPivotInfo ()
	{
		var piv = (Worm) Pivot;
		return $"choice [{piv.ToShortString ()}]";
	}

	public override IEnumerable<Worm> InvolvedWorms ()
	{
		yield return (Worm) Pivot;
	}
}
