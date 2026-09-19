using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Minotaur.Boards.Common;
using Minotaur.GenericTreeSearch;

namespace Minotaur.Boards.Dancing;

public sealed class DancingSolutionAction : TreeAction<BoardGrid>
{
	public readonly List<PentoColHead> UsedColumns;

	/// <summary>
	/// Map of pivot to column quality.
	/// A pivots can be a cell or (rarely) a piece.
	/// The int describes the shortness of the pivot column compared to the globally shortest column.
	/// </summary>
	public readonly Dictionary<PentoColHead, int> Pivots = new ();

	public readonly int [] CoveredCells;
	public readonly string PieceName;

	public DancingSolutionAction (
		IExpandMeta meta,
		DancingSolutionPivot parent,
		DancingSolutionNode child,
		int [] coveredCells,
		string pieceName)
		: base (meta)
	{
		if (child is null) {
			throw new ArgumentNullException (nameof (child));
		}
		if (parent.Parent == child) {
			throw new ArgumentException ("Parent must be different from child.", nameof (parent));
		}

		CoveredCells = coveredCells ?? throw new ArgumentNullException (nameof (coveredCells));
		PieceName = pieceName ?? throw new ArgumentNullException (nameof (pieceName));

		parent.AddChild (this);

		AddTreeChild (child);
		AfterExpansionComplete ();
	}

	public override string ToString ()
	{
		//var pivs = Pivots.Select (p => $"{p.Key}:-{p.Value}");
		//var s = $"{PieceName} {string.Join (",", WholePieceCells)} [{string.Join (", ", pivs)}]";

		var orderedGroups = Pivots
			.GroupBy (p => -p.Value)
			.OrderByDescending (g => g.Key)
			.Select (g => $"{g.Key}:{string.Join ("/", g.Select (pair => pair.Key))}");
		var pivs = string.Join ("; ", orderedGroups);
		var s = $"{PieceName} {string.Join (",", CoveredCells)} [{pivs}]";
		return s;
	}

	public override string DescriptionConsole ()
	{
		return $"{PieceName} {string.Join (",", CoveredCells)}";
	}

	public override string DescriptionLatex ()
	{
		return ToString ();
	}

	public override string DescriptionHtml ()
	{
		return ToString ();
	}

	public void AddPivot (PentoColHead pivot, int worse)
	{
		/*
		 * Note: The pivot may already exist in the dict.
		 * This happens if we arrive here from a solution permutation.
		 */
		Pivots [pivot] = worse;
	}

	protected override void CreateChildren ()
	{
		// We do this ourselves...
		throw new NotSupportedException ();
	}
}
