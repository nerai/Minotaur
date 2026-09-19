using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;

namespace Minotaur.Boards.Strings;

public sealed class WormyAction_Boardsplit : WormyAction
{
	public readonly Worm Cut;

	public readonly ImmutableArray<Worm> Reachable;

	private readonly WormyBoardGrid _NewGrid;

	public WormyAction_Boardsplit (
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
		var restrictIsInSet = reachable.Contains (cut); // This must be cached

		_NewGrid = new (parent2.Grid);
		_NewGrid.SplitBoardInplace (Cut, reachable, restrictIsInSet);
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
		return "D{" + string.Join (" + ", Reachable.Select (w => w.ToShortString ())) + $"}} < {Cut.ToShortString ()}";
	}

	public override string DescriptionLatex ()
	{
		//return $"Board subdivision via {Cut.Latex ()}\n\\strf into {string.Join (", ", Reachable.Select (w => w.Latex ()))}";
		return $"Board subdivision via {Cut.Latex ()}";
	}

	public override string DescriptionHtml ()
	{
		var s = $"There is a global bottleneck in ";
		if (Cut.Cells.Length <= 1) {
			s += $"cell {Cut.Cells.Single ()}.";
		}
		else {
			s += $"string [{Cut.ToShortString ()}].";
		}
		s += $"\n";
		var strings = string.Join (", ", Reachable.Select (w => $"[{w.ToShortString ()}]"));
		s += $"It subdivides the boards. One area consists of {strings}.";
		s += $"\n";
		return s;
	}

	public override Worm ParentOfWorm (Worm child)
	{
		// No structure change here, can just return same cells
		return child;
	}

	public override IEnumerable<Worm> HotWorms ()
	{
		yield return Cut;
	}
}
