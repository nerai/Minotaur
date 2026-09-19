using System.Collections.Immutable;
using System.Diagnostics;
using Minotaur.Boards.Common;

namespace Minotaur.Boards.Strings;

public sealed class WormyPivot_Piece : WormyPivot
{
	public readonly Piece UsedPiece;

	public WormyPivot_Piece (
		WormyNode parent,
		WormyTreeCreationMeta meta,
		Piece pivot,
		IEnumerable<(Piece piece, ImmutableArray<Worm> sourceWorms)> insertionPoints)
		:
		base (meta, double.NegativeInfinity, pivot, meta.Cost_Piece)
	{
		UsedPiece = pivot;
		// Desire will be set later, manually

		foreach (var ip in insertionPoints) {
			var action = new WormyAction_Piece (
				meta,
				this,
				parent,
				pivot,
				ip.sourceWorms);
			AddChild (action);
		}
	}

	public static IEnumerable<(Piece piece, ImmutableArray<Worm> sourceWorms)> FindInsertionPoints (
		WormyBoardGrid grid,
		Piece pivot)
	{
		var rots = pivot._UniqueRotations.Values;

		foreach (var rot in rots) {
			var x1 = grid.SX - rot._SX + 1;
			var y1 = grid.SY - rot._SY + 1;

			for (int y = 0; y < y1; y++) {
				for (int x = 0; x < x1; x++) {
					// Try to fit the piece in this place
					var worms = grid.TryInsertPiece (rot, x, y);
					if (worms == null) {
						continue;
					}

					// This should not happen: The piece should be part of a board's used pieces already
					Debug.Assert (worms.Count >= 2);

					yield return (pivot, worms.AsImmutable ());
				}
			}
		}
	}

	protected override string GetPivotInfo ()
	{
		var piv = (Piece) Pivot;
		return $"piece {piv.Name}";
	}

	public override IEnumerable<Worm> InvolvedWorms ()
	{
		foreach (var act in ChildActions.Cast<WormyAction_Piece> ()) {
			yield return act.MergedWorm;
		}
	}
}
