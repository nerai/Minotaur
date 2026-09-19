using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using Minotaur.Boards.Common;
using Minotaur.GenericTreeSearch;

namespace Minotaur.Boards.Common;

public abstract class TreePivot<TGrid>
	: AbstractTreeNode
	, IComparable<TreePivot<TGrid>>
	where TGrid : ISolutionGrid
{
	protected override char _DebugPrefix () => 'P';

	public object Pivot { get; private set; }

	public bool ExpansionComplete { get; private set; } = false;

	public TreeNode<TGrid> Parent => (TreeNode<TGrid>) TreeParents.SingleOrDefault ();

	public IEnumerable<TreeAction<TGrid>> ChildActions => AllTreeChildren.Cast<TreeAction<TGrid>> ();

	protected TreePivot (
		IExpandMeta meta,
		double desire,
		object pivot)
		: base (meta, true)
	{
		Desire = desire;
		if (double.IsNaN (desire)) {
			throw new ArgumentException ();
		}
		Pivot = pivot;
	}

	public void AddChild (TreeAction<TGrid> child)
	{
		AddTreeChild (child);
	}

	protected virtual string GetPivotInfo () => null;

	public override string ToString ()
	{
		var sb = new StringBuilder ();
		sb.Append ($"[D:{Desire:0.0}] ");
		sb.Append (GetType ().Name.Replace ("WormyPivot_", ""));

		var piv = GetPivotInfo ();
		if (piv != null) {
			sb.Append ($"; pivot {piv}");
		}

		sb.Append ($"; V {Playouts}");
		sb.Append ($"; C {MinCostToFinish} - {MaxCostToFinish}");
		if (Parent == null) {
			sb.Append ($" [pruned]");
		}
		else {
			//sb.Append ($", {AllTreeChildren.Count} chs via {Pivot}");
		}
		return sb.ToString ();
	}

	public string AsHtml ()
	{
		var sb = new StringBuilder ();
		sb.Append (GetPivotInfo ());
		sb.Append ($" (desire: {10 + Desire * 90:0}%)"); // dont confuse the reader with "0%"
		return sb.ToString ();
	}

	public int CompareTo (TreePivot<TGrid> other)
	{
		return -1 * Desire.CompareTo (other.Desire);
	}

	public override void PruneChild (AbstractTreeNode child)
	{
		//Console.WriteLine ($"PruneChild {child._DebugName}");
		base.PruneChild (child);

		var act = (TreeAction<TGrid>) child;
		foreach (var node in act.AllTreeChildren.ToArray ()) {
			act.PruneChild (node);
		}
	}
}
