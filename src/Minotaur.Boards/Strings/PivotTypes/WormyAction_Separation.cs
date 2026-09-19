using System;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Minotaur.Boards.Strings;

public sealed class WormyAction_Separation : WormyAction
{
	public readonly Worm Neighbour;

	private readonly WormyBoardGrid _NewGrid;

	public WormyAction_Separation (
		WormyTreeCreationMeta meta,
		WormyPivot parent,
		WormyNode parent2,
		Worm pivot,
		Worm neighbour)
		: base (meta, pivot)
	{
		Neighbour = neighbour ?? throw new ArgumentNullException (nameof (neighbour));
		_NewGrid = new (parent2.Grid);
		_NewGrid.SplitWormsInplace (Pivot, Neighbour);
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
		return $"{Pivot.ToShortString ()} split from {Neighbour.ToShortString ()}";
	}

	public override string DescriptionLatex ()
	{
		return $"{Pivot.Latex ()} \\strs {Neighbour.Latex ()}";
	}

	public override string DescriptionHtml ()
	{
		return $"{Pivot.ToShortString ()} is split from {Neighbour.ToShortString ()}";
	}

	public override Worm ParentOfWorm (Worm child)
	{
		// No structure change here, can just return same cells
		return child;
	}

	public override IEnumerable<Worm> HotWorms ()
	{
		yield return Pivot;
		yield return Neighbour;
	}
}
