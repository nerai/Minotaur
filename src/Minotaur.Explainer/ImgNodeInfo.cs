using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Minotaur.Boards.Common;

namespace Minotaur.Explanation;

public class ImgNodeInfo<TGrid> where TGrid : ISolutionGrid
{
	public readonly TreeNode<TGrid> Node;
	public readonly int Y;
	public readonly Dictionary<TreeAction<TGrid>, ImgNodeInfo<TGrid>> Children
		= new Dictionary<TreeAction<TGrid>, ImgNodeInfo<TGrid>> ();
	public readonly Dictionary<TreeAction<TGrid>, ImgNodeInfo<TGrid>> Parents
		= new Dictionary<TreeAction<TGrid>, ImgNodeInfo<TGrid>> ();

	public string Name;

	public SolutionTreeNodeKind Kind;

	public ImgNodeInfo (TreeNode<TGrid> node, int y)
	{
		Node = node ?? throw new ArgumentNullException (nameof (node));
		Y = y;
	}

	public void Freeze ()
	{
		if (TotalNodes.HasValue) {
			// Already frozen
			return;
		}

		var n = 1;
		var h = 1;
		foreach (var child in Children.Values) {
			child.Freeze ();
			n += child.TotalNodes.Value;
			h = Math.Max (h, 1 + child.TreeHeight.Value);
		}
		TotalNodes = n;
		TreeHeight = h;
	}

	public int? TotalNodes = null;

	/// <summary>
	/// Height of this subtree.
	/// Includes this node itself, i.e. minimum value of a leaf is 1.
	/// </summary>
	public int? TreeHeight = null;

	public override string ToString ()
	{
		return $"Info for N{Node._DebugId}, Y={Y}, Kind={Kind}, TotalContained={TotalNodes}";
	}

	public bool AncestorsAreBranchless ()
	{
		if (Parents.Count > 1) {
			return false;
		}
		if (Parents.Count == 0) {
			return true;
		}
		if (Parents.Single ().Value.Children.Count > 1) {
			return false;
		}
		return Parents.Single ().Value.AncestorsAreBranchless ();
	}

	public IEnumerable<TreeAction<TGrid>> Ancestors ()
	{
		var head = this;
		while (head.Parents.Count > 0) {
			/*
			 * Usually, there is 1 parent.
			 * But it can happen that there are multiple.
			 * This is not a bug.
			 * The board graphics rely on an arbitrary parent.
			 */
			var pair = head.Parents.First ();
			yield return pair.Key;
			head = pair.Value;
		}
	}
}
