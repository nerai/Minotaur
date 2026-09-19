using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;

namespace Minotaur.Boards.Strings;

public sealed class WormyAction_Bottleneck : WormyAction
{
	public readonly Worm Cut;

	public readonly ImmutableArray<Worm> Reachable;

	public readonly Worm MergedWorm;

	private readonly WormyBoardGrid _NewGrid;

	public WormyAction_Bottleneck (
		WormyTreeCreationMeta meta,
		WormyPivot parent,
		WormyNode parent2,
		Worm pivot,
		Worm cut,
		ImmutableArray<Worm> reachable)
		: base (meta, pivot)
	{
		Cut = cut ?? throw new ArgumentNullException (nameof (cut));
		Reachable = reachable;
		Debug.Assert (reachable.Length <= 5);

		/*
		 * Construct a worm merged from all nodes in the mincut set.
		 */
		_NewGrid = new (parent2.Grid);
		_NewGrid.MergeWormsInplace (reachable, out MergedWorm);
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
		return $"Bottleneck [{Cut.ToShortString ()}] merges {MergedWorm.ToShortString ()}";
	}

	public override string DescriptionLatex ()
	{
		return $"Bottleneck \\str{{{Cut.Latex ()}}} \\strf " + string.Join (" \\strm ", Reachable.Select (w => w.Latex ()));
	}

	public override string DescriptionHtml ()
	{
		var s = $"There is a bottleneck in ";
		if (Cut.Cells.Length <= 1) {
			s += $"cell {Cut.Cells.Single ()}.";
		}
		else {
			s += $"string [{Cut.ToShortString ()}].";
		}
		s += $"\n";

		if (Reachable.Length <= 1) {
			s += $"It restricts [{Reachable.Single ().ToShortString ()}].";
		}
		else {
			var strings = string.Join (", ", Reachable.Select (w => $"[{w.ToShortString ()}]"));
			s += $"It restricts {strings}.";
		}
		s += $"\n";

		s += $"This forces merging them into the new string [{MergedWorm.ToShortString ()}]";
		s += $"\n";

		return s;
	}

	public override Worm ParentOfWorm (Worm child)
	{
		foreach (var cell in child.Cells) {
			if (MergedWorm.Cells.Contains (cell)) {
				return Reachable
					.OrderByDescending (w => w.Cells.Length)
					.ThenBy (w => w.Cells.Min ())
					.First ();
			}
		}

		return child;
	}

	public override IEnumerable<Worm> HotWorms ()
	{
		yield return MergedWorm;
	}
}
