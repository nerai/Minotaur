using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using Minotaur.Boards.Strings;
using Minotaur.GenericTreeSearch;

namespace Minotaur.Boards.Common;

public abstract class TreeAction<TGrid>
	: AbstractTreeNode
	where TGrid : ISolutionGrid
{
	protected override char _DebugPrefix () => 'A';

	public TreePivot<TGrid> Parent => (TreePivot<TGrid>) TreeParents.Single ();

	public TreeNode<TGrid> Child => (TreeNode<TGrid>) AllTreeChildren.Single ();

	/// <summary>
	/// Very short description of this action, around 10-20 characters.
	/// </summary>
	public abstract string DescriptionConsole ();

	/// <summary>
	/// Very short description of this action, around 10-20 characters, for display in latex/tikz.
	/// </summary>
	public abstract string DescriptionLatex ();

	/// <summary>
	/// Detailed, verbose description of this action, made for human consumption, for display in HTML.
	/// </summary>
	public abstract string DescriptionHtml ();

	public override string AsStringInline () => DescriptionConsole ();

	protected TreeAction (IExpandMeta meta)
		: base (meta, true)
	{ }

	public override void PruneChild (AbstractTreeNode child)
	{
		//Console.WriteLine ($"PruneChild {child._DebugName}");
		base.PruneChild (child);

		var node = (TreeNode<TGrid>) child;
		if (node.Parents.Any ()) {
			// Node still has other parents
			return;
		}
		//Console.WriteLine ($"{node._DebugName} has no parents anymore");
		if (node._Meta is WormyTreeCreationMeta meta) {
			meta.Pool.ReturnNode (node as WormyNode);
			/*
			 * The entire subtree rooted in this node will be removed by the GC.
			 * That will in turn call the dtors that will return the node to the pool.
			 */
		}
	}
}
