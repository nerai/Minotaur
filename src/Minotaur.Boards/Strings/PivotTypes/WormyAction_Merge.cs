using System;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Minotaur.Boards.Strings;

public sealed class WormyAction_Merge : WormyAction
{
	public readonly Worm Neighbour;

	public readonly Worm MergedWorm;

	private readonly WormyBoardGrid _NewGrid;

	public WormyAction_Merge (
		WormyTreeCreationMeta meta,
		WormyPivot parent,
		WormyNode parent2,
		Worm pivot,
		Worm neighbour)
		: base (meta, pivot)
	{
		Neighbour = neighbour ?? throw new ArgumentNullException (nameof (neighbour));
		_NewGrid = new (parent2.Grid);
		_NewGrid.MergeWormsInplace (
			new [] { Pivot, Neighbour },
			out MergedWorm);
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
		return $"{Pivot.ToShortString ()} merges with {Neighbour.ToShortString ()}";
	}

	public override string DescriptionLatex ()
	{
		return $"{Pivot.Latex ()} \\strm {Neighbour.Latex ()}";
	}

	public override string DescriptionHtml ()
	{
		return $"[{Pivot.ToShortString ()}] merges with [{Neighbour.ToShortString ()}]";
	}

	public override Worm ParentOfWorm (Worm child)
	{
		foreach (var cell in child.Cells) {
			if (MergedWorm.Cells.Contains (cell)) {
				// return bigger source
				if (Pivot.Cells.Length >= Neighbour.Cells.Length) {
					return Pivot;
				}
				else {
					return Neighbour;
				}
			}
		}

		return child;
	}

	public override IEnumerable<Worm> HotWorms ()
	{
		yield return MergedWorm;
	}
}
