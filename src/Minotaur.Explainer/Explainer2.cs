using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Minotaur.Boards.Common;
using Minotaur.Boards.Dancing;
using Minotaur.Boards.Drawing;
using Minotaur.Boards.Strings;
using Minotaur.Utils;

namespace Minotaur.Explanation;

public class Explainer2<TGrid> where TGrid : ISolutionGrid
{
	public readonly TreeNode<TGrid> _Root;
	public readonly string _RootDir;
	public readonly Dictionary<TreeNode<TGrid>, ImgNodeInfo<TGrid>> _NodeLookup;
	public readonly int _RowCount;

	public static bool PrintProgress = 1111 == 111;

	public Explainer2 (
		TreeNode<TGrid> treeRoot,
		string rootDir)
	{
		_Root = treeRoot;
		_RootDir = rootDir;

		if (!_Root.IsSolvable.Value) {
			Debugger.Launch ();
			Debugger.Break ();
			Console.WriteLine (treeRoot.TreeAsString ());
			throw new ArgumentException ();
		}

		using var to = new TimedOperation ($"Building explanation lookup tables", 10);
		_NodeLookup = new ();
		_RowCount = 0;

		/*
		 * Iterate over all nodes, one depth level per iteration.
		 * 
		 * These lists are ordered lists of nodes and links.
		 * They are ordered from the root, row by row, down to leafs.
		 */
		if (PrintProgress) {
			Console.WriteLine ($"Creating nodes...");
		}
		var nodesToCreate = new List<TreeNode<TGrid>> { treeRoot };
		var linksToCreate = new List<TreeAction<TGrid>> ();

		while (nodesToCreate.Any ()) {
			if (PrintProgress) {
				Console.WriteLine ($"Row {_RowCount} has {nodesToCreate.Count} nodes");
			}
			foreach (var it in nodesToCreate) {
				/*
				var isNew = parentLookup.ContainsKey (it);
				Console.WriteLine ($"Level {rowCount}, old? {isNew}, node {it.Grid.UniqueGridHash ()}");
				foreach (var a in it.Children) {
					Console.WriteLine (a.ShortDescription () + $" --> display {ShouldDisplayAction (a)}");
				}
				*/
				Debug.Assert (it.SolvabilityIsKnown);
				Debug.Assert (it.TreeNodeRequiresAllChildren || it.AllTreeChildren.Count <= 1);
				_NodeLookup [it] = new ImgNodeInfo<TGrid> (it, _RowCount);
			}

			_RowCount++;
			var validLinks = nodesToCreate
				.SelectMany (n => n.Pivots.SelectMany (piv => piv.ChildActions)) // TODO: ggf pivots als eigene zeile??
				.ToList ();
			linksToCreate.AddRange (validLinks);
			nodesToCreate = validLinks
				.Select (a => a.Child)
				.Distinct ()
				.ToList ();
		}

		/*
		 * Insert links.
		 * This was not possible earlier because nodes can be updated/replaced if the same node is accessed in a different row.
		 */
		if (PrintProgress) {
			Console.WriteLine ($"Inserting {linksToCreate.Count} links...");
		}
		foreach (var link in linksToCreate.Distinct ()) {
			var parent = _NodeLookup [link.Parent.Parent];
			var child = _NodeLookup [link.Child];
			parent.Children.Add (link, child);
			child.Parents.Add (link, parent);
		}

		/*
		 * Freeze to calculate counts
		 */
		if (PrintProgress) {
			Console.WriteLine ($"Freezing {_NodeLookup.Count} nodes...");
		}
		_NodeLookup [_Root].Freeze ();
		var nodesGroupedAndOrdered = _NodeLookup
			.GroupBy (pi => pi.Value.Y)
			.OrderBy (gr => gr.Key)
			.ToList ();

		/*
		 * Name solvable nodes
		 */
		if (PrintProgress) {
			Console.WriteLine ($"Naming solvable nodes...");
		}
		var nameWrongBranchesWork = new Queue<ImgNodeInfo<TGrid>> ();
		foreach (var gr in _NodeLookup.GroupBy (pi => pi.Value.Y)) {
			var y = gr.Key;
			var nodesInRow = gr
				.Select (pair => pair.Value)
				.OrderBy (node => node.Node._DebugId)
				.ToList ();
			var solvable = nodesInRow.Where (pi => pi.Node.IsSolvable.Value).ToList ();
			foreach (var pi in solvable) {
				var par = pi.Parents.Values
					.OrderBy (parent => parent.Node._DebugId)
					.FirstOrDefault ();
				nameWrongBranchesWork.Enqueue (pi);
				if (par == null) {
					pi.Kind = SolutionTreeNodeKind.Start;
					pi.Name = "Start";
					continue;
				}

				if (solvable.Count <= 1) {
					if (par.Children.Count == 1) {
						pi.Kind = SolutionTreeNodeKind.Single_Correct;
					}
					else {
						pi.Kind = SolutionTreeNodeKind.Decision_Single_Correct;
					}
					pi.Name = $"M{y}";
				}
				else {
					pi.Kind = SolutionTreeNodeKind.Other;
					pi.Name = $"M{y}.{solvable.IndexOf (pi)}";
				}
			}
		}

		if (PrintProgress) {
			Console.WriteLine ($"Naming wrong branches...");
		}
		while (nameWrongBranchesWork.TryDequeue (out var pi)) {
			//Console.WriteLine ($"Naming branches, {nameWrongBranchesWork.Count} nodes remain");
			Debug.Assert (pi.Name != null);
			var prefix = pi.Name;
			int suffix = 0;
			var split = pi.Name.LastIndexOf ("/");
			if (split >= 0) {
				prefix = pi.Name.Substring (0, split);
				suffix = int.Parse (pi.Name.Substring (split + 1));
			}

			var ch = pi.Children.Values
				.Where (c => !c.Node.IsSolvable.Value)
				.ToList ();
			for (int i = 0; i < ch.Count; i++) {
				var c = ch [i];
				if (c.Name != null) {
					continue;
				}

				nameWrongBranchesWork.Enqueue (c);

				if (pi.Node.IsSolvable.Value) {
					if (ch.Count == 1) {
						c.Kind = SolutionTreeNodeKind.Decision_Single_Wrong;
						c.Name = $"{prefix}-Fail/0";
					}
					else {
						c.Kind = SolutionTreeNodeKind.Other;
						c.Name = $"{prefix}-Fail-{Util.Int2Base26 (i)}/0";
					}
				}
				else {
					if (ch.Count == 1) {
						c.Kind = SolutionTreeNodeKind.Single_Wrong;
						c.Name = $"{prefix}/{suffix + 1}";
					}
					else {
						c.Kind = SolutionTreeNodeKind.Decision_All_Wrong;
						c.Name = $"{prefix}/{suffix}-{Util.Int2Base26 (i)}/0";
					}
				}
			}
		}
	}
}
