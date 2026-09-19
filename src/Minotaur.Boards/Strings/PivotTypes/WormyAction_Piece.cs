using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using Minotaur.Boards.Common;

namespace Minotaur.Boards.Strings;

public sealed class WormyAction_Piece : WormyAction
{
	public readonly ImmutableArray<Worm> SourceWorms;

	public readonly Worm MergedWorm;

	private readonly WormyBoardGrid _NewGrid;

	private WormyPivot_Piece CastParent => (WormyPivot_Piece) Parent;

	public WormyAction_Piece (
		WormyTreeCreationMeta meta,
		WormyPivot parent,
		WormyNode parent2,
		Piece pivot,
		ImmutableArray<Worm> sourceWorms)
		: base (meta, sourceWorms.MaxBy (w => w.Cells.Length))
	{
		SourceWorms = sourceWorms;
		_NewGrid = new (parent2.Grid);
		_NewGrid.MergeWormsInplace (SourceWorms, out MergedWorm);
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
		var sources = string.Join (", ", SourceWorms.Select (w => $"{w}"));
		return $"Piece {CastParent.UsedPiece.Name} in {sources}";
	}

	public override string DescriptionLatex ()
	{
		return $"\\piece{{{CastParent.UsedPiece.Name}}} placed as {MergedWorm.Latex ()}";
	}

	public override string DescriptionHtml ()
	{
		return $"Piece {CastParent.UsedPiece.Name} is placed as [{MergedWorm.ToShortString ()}]";
	}

	public override Worm ParentOfWorm (Worm child)
	{
		foreach (var cell in child.Cells) {
			if (MergedWorm.Cells.Contains (cell)) {
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
