using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using Minotaur.Utils;

namespace Minotaur.Boards.Strings;

public sealed class WormyAction_Choice : WormyAction
{
	public readonly Worm Neighbour;

	public readonly ImmutableArray<Worm> Refused;

	public readonly Worm MergedWorm;

	private readonly WormyBoardGrid _NewGrid;

	public WormyAction_Choice (
		WormyTreeCreationMeta meta,
		WormyPivot parent,
		WormyNode parent2,
		Worm pivot,
		Worm neighbour,
		SmallSet<Worm> allNeighbors)
		: base (meta, pivot)
	{
		Neighbour = neighbour;

		/*
		 * Note: It is not required to worry about the not-chosed (refused)
		 * neighbours (e.g. by adding them as separated).
		 * A choice is made only when there is exactly one option that can be picked.
		 * This means that once an option was chosen, the cell count of the pivot raises
		 * high enough that a connection to any other option is no longer possible.
		 * The other options will thus be removed naturally (elsewhere), and there is no
		 * need to remove them here.
		 * This means that this case becomes equivalent to the connect-case of a connect/split decision.
		 */
		_NewGrid = new (parent2.Grid);
		_NewGrid.MergeWormsInplace (
			new [] { pivot, neighbour },
			out MergedWorm);

		/*
		 * We still create the refused set for display purposes.
		 */
		var refused = new SmallSet<Worm> (allNeighbors);
		refused.Remove (neighbour);
		Refused = refused.AsImmutable ();
	}

	protected override void CreateChildren ()
	{
		var child = Meta.Pool.GetOrCreate (_NewGrid, this);
		Debug.Assert (child != Parent.Parent);
		AddTreeChild (child);
	}

	public override string ToString ()
	{
		throw new NotImplementedException ();
	}

	public override string DescriptionConsole ()
	{
		return $"{Pivot.ToShortString ()} chooses {Neighbour.ToShortString ()}";
	}

	public override string DescriptionLatex ()
	{
		return $"{Pivot.Latex ()} chooses {Neighbour.Latex ()}";
	}

	public override string DescriptionHtml ()
	{
		return $"[{Pivot.ToShortString ()}] chooses [{Neighbour.ToShortString ()}]";
	}

	public override Worm ParentOfWorm (Worm child)
	{
		foreach (var cell in child.Cells) {
			if (MergedWorm.Cells.Contains (cell)) {
				// Always pivot, never one of the added strings
				return Pivot;
			}
		}

		return child;
	}

	public override IEnumerable<Worm> HotWorms ()
	{
		yield return MergedWorm;
	}
}
