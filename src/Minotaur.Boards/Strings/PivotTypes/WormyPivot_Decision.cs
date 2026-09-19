namespace Minotaur.Boards.Strings;

public sealed class WormyPivot_Decision : WormyPivot
{
	public WormyPivot_Decision (
		WormyNode parent,
		WormyTreeCreationMeta meta,
		Worm pivot,
		Worm neighbour,
		double desire)
		:
		base (meta, desire, (pivot, neighbour), meta.Cost_Decision)
	{
		/*
		 * Case: They are separate.
		 * This is only interesting if there are several options how to merge the pivot.
		 */
		{
			var action = new WormyAction_Separation (
				meta,
				this,
				parent,
				pivot,
				neighbour);
			AddChild (action);
		}

		/*
		 * Case: They are connected
		 */
		{
			var action = new WormyAction_Merge (
				meta,
				this,
				parent,
				pivot,
				neighbour);
			AddChild (action);
		}
	}

	protected override string GetPivotInfo ()
	{
		var piv = ((Worm a, Worm b)) Pivot;
		return $"split/merge [{piv.a.ToShortString ()}] ? [{piv.b.ToShortString ()}]";
	}

	public Worm [] MergedWorms {
		get {
			var piv = ((Worm a, Worm b)) Pivot;
			return new [] { piv.a, piv.b };
		}
	}

	public override IEnumerable<Worm> InvolvedWorms ()
	{
		yield return ChildActions.OfType<WormyAction_Merge> ().Single ().MergedWorm;
	}
}
