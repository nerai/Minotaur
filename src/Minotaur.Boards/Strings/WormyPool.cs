using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Minotaur.Boards.Common;
using Minotaur.GenericTreeSearch;
using Minotaur.Utils;

namespace Minotaur.Boards.Strings;

public sealed class WormyPool
{
	private readonly Dictionary<ImmutableArray<byte>, WormyNode> _Nodes = new (ByteArrayEqualityComparer.Instance);

	private ulong _Requested = 0;
	private ulong _Created = 0;
	private ulong _Returned = 0;
	private ulong _Destroyed = 0;
	private ulong _Abandoned = 0;

	public SaturatingCost EnabledBelowCost = new SaturatingCost (199ul); //xxx

	public bool PoolIsInUse => _Nodes.Count > 0;

	/// <summary>
	/// Threadsafe get existing node for given hash, or create and add new node.
	/// </summary>
	public TreeNode<WormyBoardGrid> GetOrCreate (WormyBoardGrid grid, TreeAction<WormyBoardGrid> commissioner)
	{
		if (commissioner.MinCostFromRoot < EnabledBelowCost) {
			_Requested++;
			if (_Requested % 10000 == 0) {
				PrintStats ();
			}
			var hash = grid.GetUniqueHash ();

			// todo concurrentdictionary
			lock (_Nodes) {
				if (!_Nodes.TryGetValue (hash, out var node)) {
					node = new WormyNode ((WormyTreeCreationMeta) commissioner._Meta, grid);
					_Nodes.Add (hash, node);
					_Created++;
				}
				++node.UsageCounter;
				return node;
			}
		}
		else {
			return new WormyNode ((WormyTreeCreationMeta) commissioner._Meta, grid);
		}
	}

	public void ReturnNode (WormyNode node)
	{
		--node.UsageCounter;
		if (node.UsageCounter < 0) {
			// Should be a root node ctor-ed elsewhere
			//Debug.Assert (!node.Parents.Any ());
			// This not most likely was not in the tree, and it did not affect our stats. Just leave here.
			return;
		}

		_Returned++;
		Debug.Assert (_Returned <= _Requested);

		Debug.Assert (node.UsageCounter >= 0);
		if (node.UsageCounter > 0) {
			return;
		}

		_Abandoned++;
		// Debug.Assert (node.Parents.Count () == 0); // This is not necessary: The entire tree can be dtor-ed.

		if (node.PrunedNodeCount >= 1) {
			/*
			 * This node is rather valueable. Keep it, even though it was abandoned.
			 * The parameter may be changed, if too many nodes are kept.
			 */
		}
		else {
			_Destroyed++;
			var hash = node.Grid.GetUniqueHash ();
			lock (_Nodes) {
				var removed = _Nodes.Remove (hash);
				Debug.Assert (removed);
				Debug.Assert (_Destroyed <= _Created);
				Debug.Assert (_Created - _Destroyed == (ulong) _Nodes.Count);
			}
		}
	}

	public void PrintStats ()
	{
		lock (_Nodes) {
			Console.WriteLine (
				$"WormyPool has {_Nodes.Count:#,##0} nodes." +
				$" Requested: {_Requested:#,##0}" +
				$", created: {_Created:#,##0}" +
				$", returned: {_Returned:#,##0}" +
				$", abandoned: {_Abandoned:#,##0}" +
				$", destroyed: {_Destroyed:#,##0}");
		}
	}
}
