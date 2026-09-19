
//#define DEBUG_PRINT

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ANSIConsole;
using Minotaur.Utils;
using static Minotaur.Utils.Logging;

namespace Minotaur.GenericTreeSearch;

[DebuggerDisplay ("{_DebugName}: P={Playouts}; C={MinCostFromRoot}+{LocalCost}+{MinCostToFinish}:{MaxCostToFinish}; {ToString()}")]
public abstract class AbstractTreeNode
{
	protected virtual char _DebugPrefix () => '@';

	[Conditional ("DEBUG_PRINT")]
	protected static void DbgLn (string text)
	{
		LogN (text);
	}

	private static long _NextDebugId = 0;
	public readonly long _DebugId = Interlocked.Increment (ref _NextDebugId);
	public string _DebugName => $"{_DebugPrefix ()}{_DebugId}";
	public string _DebugDisplayString =>
		$"{_DebugName}: " +
		$"P={Playouts}; " +
		$"C={MinCostFromRoot}+{LocalCost}+{MinCostToFinish}:{MaxCostToFinish}; " +
		$"{(IsExpanded ? $"{_Children.Count} ch" : "n.ex")}; " +
		$"{ToString ()}";

	public readonly IExpandMeta _Meta;

	internal List<AbstractTreeNode> _Parents = new ();
	internal List<AbstractTreeNode> _Children = new ();

	protected IReadOnlyList<AbstractTreeNode> TreeParents => _Parents;
	protected IEnumerable<AbstractTreeNode> ExpandedChildren => _Children.Where (ch => ch.IsExpanded);
	public IReadOnlyCollection<AbstractTreeNode> AllTreeChildren => _Children;

	public static bool StorePruned = false;
	public List<AbstractTreeNode> PrunedChildren = StorePruned ? new (0) : null;

	public bool IsExpanded { get; private set; } = false;

	public ulong Playouts = 0;

	/// <summary>
	/// How likely this node is to be chosen in a random playout.
	/// This is different from the cost: A node can be desired even though it is expensive, and the other way around.
	/// Desire measures how likely this node is to lead to an overall cheap result.
	/// </summary>
	public double Desire { get; set; } = 0.0;

	public SaturatingCost LocalCost { get; protected set; } = 1;

	/// <summary>
	/// Cost from origin to this node, excluding the local cost.
	/// Initially infinity, decreased by arriving at this node with a lower path cost during forward propagation.
	/// </summary>
	public SaturatingCost MinCostFromRoot { get; private set; } = SaturatingCost.Infinity;

	/// <summary>
	/// Minimum cost to finish from this node, excluding its local cost.
	/// Initially zero, increased by not finding a complete solution within a certain cost distance.
	/// This is a lower bound, the real cost may be higher.
	/// </summary>
	public SaturatingCost MinCostToFinish { get; private set; } = SaturatingCost.Zero;

	/// <summary>
	/// Maximum cost to finish from this node, excluding its local cost.
	/// Initially infinity, decreased by finding any complete solution path.
	/// This is an upper bound, the real cost may be lower.
	/// </summary>
	public SaturatingCost MaxCostToFinish { get; private set; } = SaturatingCost.Infinity;

	public void SetRoot ()
	{
		MinCostFromRoot = 0;
	}

	/// <summary>
	/// True if this is a leaf and solution,
	/// False if this is a leaf and not a solution,
	/// Null if this is not a leaf.
	/// </summary>
	public bool? IsSolution {
		get {
			if (_IsSolution.HasValue) {
				Debug.Assert (_Children.Count == 0);
			}
			return _IsSolution;
		}
	}
	private bool? _IsSolution;

	protected void SetIsSolution (bool isSolution)
	{
		Debug.Assert (_Children.Count == 0);
		_IsSolution = isSolution;
		_IsSolvable = isSolution;
		MinCostToFinish = 0;
		MaxCostToFinish = 0;
	}

	public bool? IsSolvable {
		get => _IsSolvable;
	}
	private bool? _IsSolvable;

	/// <summary>
	/// This is different from an AND-node.
	/// It does NOT require all children to be solvable -- that is orthogonal.
	/// Instead, it requires the solvability status of all children to be known.
	/// 
	/// That means that unsolvable branches must be explored completely.
	/// Solvable branches must have a path to a solution, it does not matter which.
	/// </summary>
	public readonly bool TreeNodeRequiresAllChildren;

	public bool PruningComplete => TreeNodeRequiresAllChildren || _Children.Count <= 1;


	protected AbstractTreeNode (
		IExpandMeta meta,
		bool requireAllChildSolvabilities)
	{
		_Meta = meta;
		TreeNodeRequiresAllChildren = requireAllChildSolvabilities;
	}

	/// <summary>
	/// Remove children and set node as "not expanded".
	/// However, if this node is a leaf (solution/dead end), do nothing.
	/// </summary>
	public virtual void UndoExpansionIfRequired ()
	{
		if (!IsExpanded) {
			Debug.Assert (_Children.Count == 0);
			return;
		}
		if (IsSolution.HasValue) {
			Debug.Assert (_Children.Count == 0);
			return;
		}

		foreach (var child in _Children.ToArray ()) {
			PruneChild (child);
		}
		Debug.Assert (_Children.Count == 0);
		IsExpanded = false;
	}

	protected abstract void CreateChildren ();

	public bool Expand ()
	{
		if (IsExpanded) {
			return false;
		}
		CreateChildren ();
		Debug.Assert (!IsExpanded);
		AfterExpansionComplete ();
		return true;
	}

	public void AfterExpansionComplete ()
	{
		if (IsExpanded) {
			throw new InvalidOperationException ();
		}

		/*
		 * Normalize the desire
		 */
		if (_Children.Count > 0) {
			var maxDesire = _Children.Max (ch => ch.Desire);
			var minDesire = _Children.Min (ch => ch.Desire);

			var epsilonTotal = 0.001;
			var epsilonEach = epsilonTotal / _Children.Count;

			foreach (var ch in _Children) {
				var v = ch.Desire;
				var normalized =
					(v - minDesire + epsilonEach)
					/
					(maxDesire - minDesire + epsilonTotal);
				Debug.Assert (!double.IsNaN (normalized));
				Debug.Assert (double.IsFinite (normalized));
				Debug.Assert (normalized > 0);
				ch.Desire = normalized;
			}
		}

		_Children.Sort ((c1, c2) => c2.Desire.CompareTo (c1.Desire));

		IsExpanded = true;
	}

	protected void AddTreeChild (AbstractTreeNode child)
	{
		if (IsExpanded) {
			throw new InvalidProgramException ();
		}

		_Children.Add (child);
		child._Parents.Add (this);
	}

	/// <summary>
	/// Perform a playout, i.e. construct the tree to reach all required leaf nodes.
	/// This is exponentially expensive!
	/// </summary>
	/// <returns>
	/// Number of nodes that were expanded
	/// If this is 0, the tree is complete.
	/// </returns>
	public uint RandomPlayout (bool onlyBestDesire)
	{
		uint nNodesCreated = 0;

		if (Expand ()) {
			nNodesCreated = 1;
		}

		var prev = double.MaxValue;
		foreach (var child in AllTreeChildren) {
			Debug.Assert (child.Desire <= prev);
			Debug.Assert (child.Desire <= 1);
			Debug.Assert (child.Desire >= 0);
			prev = child.Desire;
		}

		if (TreeNodeRequiresAllChildren) {
			/*
			 * Playout ALL children
			 */
			var chs = _Children.ToArray ();
			foreach (var ch in chs) {
				if (!ch._Parents.Contains (this)) {
					// This child was pruned
					Debug.Assert (!_Children.Contains (ch));
					continue;
				}
				nNodesCreated += ch.RandomPlayout (onlyBestDesire);
			}
		}
		else if (onlyBestDesire) {
			if (AllTreeChildren.Count > 0) {
				// Children are sorted by desire
				var bests = AllTreeChildren.TakeWhile (ch => ch.Desire >= AllTreeChildren.First ().Desire);
				var best = onlyBestDesire
					? bests.First ()
					: bests.Shuffled ().First ();
				nNodesCreated += best.RandomPlayout (onlyBestDesire);
				Debug.Assert (AllTreeChildren.All (c => c.Desire <= best.Desire));
			}
		}
		else {
			/*
			 * Playout a random node, weighted by desire
			 */
			var r = Util.ThreadRandom.Value;
			var chs = AllTreeChildren.ToList ();

			/*
			 * Avoid branches that are fully pruned.
			 * The tree search should guarantee this does not happen.
			 */
			//chs.RemoveAll (ch => ch.PruningComplete && ch.MinCostToFinish == ch.MaxCostToFinish);

			// todo: das kann man mit einem angepassen shuffle in O(n) machen
			while (true) {
				if (chs.Count == 0) {
					break;
				}

				var desireSum = 0.0;
				var pick = chs.First ();
				foreach (var ch in chs) {
					var v = ch.Desire;
					Debug.Assert (v >= 0);
					Debug.Assert (v <= 1);
					desireSum += v;
					Debug.Assert (desireSum >= 0.0);
					if (r.NextDouble () * desireSum < v) {
						pick = ch;
					}
				}

				nNodesCreated += pick.RandomPlayout (onlyBestDesire);
				if (nNodesCreated == 0) {
					// This child failed to produce a playout. Remote it so another can take its place.
					chs.Remove (pick);
					continue;
				}

				/*
				// Undo
				foreach (var ch in pick._Children) {
					if (ch._Parents.Count <= 1) {
						ch.UndoExpansionIfRequired ();
					}
				}
				*/

				break;
			}
		}

		Backpropagate ();
		ForwardPropagate ();

		return nNodesCreated;
	}

	private void Backpropagate ()
	{
		if (!IsExpanded) {
			return;
		}

		var anythingChanged = false;

		if (_Children.Count == 0) {
			if (!TreeParents.Any ()) {
				// This node was pruned
			}
			else {
				Debug.Assert (IsSolvable.HasValue);
				Debug.Assert (IsSolution.HasValue);
				Debug.Assert (MinCostToFinish == SaturatingCost.Zero);
				Debug.Assert (MaxCostToFinish == SaturatingCost.Zero);
				anythingChanged = true; // Force this in leaf nodes
			}
		}
		else {
			Debug.Assert (!IsSolution.HasValue);

			/*
			 * Update solvability
			 * 
			 * This node is solvable if any child is solvable.
			 * Note that the current node is not a leaf node.
			 * 
			 * If _RequireAllChildSolvabilities, then at most one child is solvable.
			 * 
			 * Else, the solvability of each child is equal (all are or all are
			 * not), so knowing one suffices to determine this property.
			 */
			if (IsSolvable.HasValue) {
				if (IsSolvable.Value) {
					Debug.Assert (_Children.Any (ch => ch.IsSolvable == true));
					if (!TreeNodeRequiresAllChildren) {
						Debug.Assert (_Children.All (ch => ch.IsSolvable != false));
					}
				}
				else {
					Debug.Assert (_Children.All (ch => ch.IsSolution != true));
					if (TreeNodeRequiresAllChildren) {
						Debug.Assert (_Children.All (ch => ch.IsSolvable == false));
					}
					else {
						Debug.Assert (_Children.All (ch => ch.IsSolvable != true));
					}
				}
			}
			else {
				if (TreeNodeRequiresAllChildren) {
					if (_Children.All (ch => ch.IsSolvable == false)) {
						_IsSolvable = false;
						anythingChanged = true;
					}
					else {
						foreach (var ch in _Children) {
							if (ch.IsSolvable == true) {
								_IsSolvable = true;
								anythingChanged = true;
								break;
							}
						}
					}
				}
				else {
					foreach (var ch in _Children) {
						if (ch.IsSolvable.HasValue) {
							_IsSolvable = ch.IsSolvable.Value;
							anythingChanged = true;
							break;
						}
					}
				}
			}

			/*
			 * Update minimum cost, i.e. the minimum cost of all possible paths.
			 * This is 0 is this node is a leaf, else >= 1.
			 * It can only grow > 1 if all children have a min cost of >= 1.
			 */
			SaturatingCost minCostToFinish;
			var minVia = "";
			if (TreeNodeRequiresAllChildren) {
				minCostToFinish = SaturatingCost.Zero;
				foreach (var ch in _Children) {
					minCostToFinish += ch.LocalCost + ch.MinCostToFinish;
				}
			}
			else {
				minCostToFinish = SaturatingCost.Infinity;
				foreach (var ch in _Children) {
					var localMinCostToFinish = ch.LocalCost + ch.MinCostToFinish;
					minCostToFinish = SaturatingCost.Min (minCostToFinish, localMinCostToFinish);
					minVia = $", via {ch._DebugName}"; // todo: only create string in debugprint
				}
			}
			// There must be a potential goal in less than infinite distance
			Debug.Assert (minCostToFinish < SaturatingCost.Infinity);
			// The minimum cost can only grow, it can never shrink
			Debug.Assert (minCostToFinish >= MinCostToFinish);
			if (minCostToFinish > MinCostToFinish) {
				DbgLn ($"{_DebugName} Update MinCostToFinish = {minCostToFinish}, was {MinCostToFinish}{minVia}");
				MinCostToFinish = minCostToFinish;
				anythingChanged = true;
			}
			Debug.Assert (MinCostToFinish < SaturatingCost.Infinity);

			/*
			 * Update maximum cost, i.e. best known path.
			 */
			if (TreeNodeRequiresAllChildren) {
				var sum = SaturatingCost.Zero;
				foreach (var ch in _Children) {
					sum += ch.LocalCost + ch.MaxCostToFinish;
				}
				if (sum < MaxCostToFinish) {
					DbgLn ($"{_DebugName} Update MaxCostToFinish = {sum}, was {MaxCostToFinish}");
					MaxCostToFinish = sum;
					anythingChanged = true;
				}
			}
			else {
				foreach (var ch in _Children) {
					var localMaxCostToFinish = ch.LocalCost + ch.MaxCostToFinish;
					// Note: localMaxCostToFinish > MaxCostToFinish can happen: It means this pivot is not optimal
					if (localMaxCostToFinish < MaxCostToFinish) {
						DbgLn ($"{_DebugName} Update MaxCostToFinish = {localMaxCostToFinish}, was {MaxCostToFinish}, via {ch._DebugName}");
						MaxCostToFinish = localMaxCostToFinish;
						anythingChanged = true;
					}
				}
			}
			// MaxCostToFinish may be infinite
			Debug.Assert (MinCostToFinish <= MaxCostToFinish);

			/*
			 * Alpha-beta-like pruning
			 */
			/*
			 * NOTE: Global pruning in a dynamic DAG is hard.
			 * Global pruning: Prune deep inside child branches based on an ancesters MaxCostToFinish value.
			 * Dynamic DAG: multiple parents, and parents can be added later.
			 * 
			 * Why try it at all?
			 * Idea: Use threshold information available at an ancestor to prune inside descendant nodes.
			 * Example:
			 * branch A total cost 5
			 * branch B total cost 3 to 8
			 * branch B.0 total cost 4 to 8
			 * branch B.0.0 total cost 5 to 8 <-- this should be pruned, but it does not know of branch A!
			 * branch B.0.0.0 total cost 6 to 8
			 * 
			 * However, it is difficult.
			 * Linking a node through a different parent may make previous non-local(!) pruning invalid.
			 * Example:
			 * 
			 * Root
			 *  |- A: 15 to 28
			 *  |- B: 20 to 40
			 *  |  `-- BA: 25 to 40
			 *  |      `-- BAA: 30 to 40
			 *  `- C: 0 to 15
			 *     `-- BA: 1 to 21 (same BA as above!)
			 *         `-- BAA: 2 to 22
			 * Prune BAA (as intended because 30 > 28)
			 * 
			 * But: Later discover a better link from C to BA:
			 * Root
			 *  |- A: 15 to 28
			 *  |- B: 20 to 40
			 *  |  `-- BA: 25 to 40
			 *  |      `-- BAA: 30 to 40 [PRUNED]
			 *  `- C: 0 to 15
			 *     `-- BA: 1 to 21
			 *         `-- BAA: 6 to 26 [PRUNED but should be here!]
			 * Now BAA is a good solution candidate, but it was pruned earlier.
			 * This violates the goal.
			 * 
			 * To prevent this:
			 * 
			 * a) If AB-pruning is used with strict DFS (no BFS), then only local pruning is ever used.
			 * Conflicts with Monte Carlo playouts, and requires good ordering heuristics to not suffer from late local pruning.
			 * 
			 * b) If the data are in a tree instead of a DAG, then a parent cannot be added later.
			 * Massive duplication of work.
			 * 
			 * c) Do not use global pruning.
			 * Some branches can be very deep, already beyong the global threshold, but do not know of this.
			 * Partially preventable via a threshold used in playouts, but complicated to implement.
			 */
			Debug.Assert (_Children.Count >= 1);
			if (TreeNodeRequiresAllChildren) {
				// No pruning
			}
			else {
				var chs = _Children
					.OrderByDescending (ch => ch.LocalCost + ch.MinCostToFinish)
					.ToList ();
				var bestLocal = chs.MinBy (ch => ch.LocalCost + ch.MaxCostToFinish);

				foreach (var ch in chs) {
					if (ch == bestLocal) {
						// Best child must always be kept
						continue;
					}

					var localMinCost = ch.LocalCost + ch.MinCostToFinish;
					if (localMinCost < MaxCostToFinish) {
						// This node can still improve the result
						continue;
					}

					/*
					 * Regular, local AB pruning:
					 * In the best case, this node is as good as the best pivot is in the worse case.
					 * This means it cannot improve the result and should be discarded.
					 * 
					 * Note: Do NOT just use child.MinCostFromRoot here.
					 * It is not updated yet, but more importantly, it is also logically wrong:
					 * The child may have a better parent, but it may still be required to be pruned from this parent node.
					 */
					DbgLn (
						$"{_DebugName}/{ch._DebugName} gets AB pruned: " +
						$"child min total cost {localMinCost} vs Parent.MaxCostToFinish {MaxCostToFinish}"
						);
					//Console.WriteLine ($"---PRUNE---\n{ch.FindRoot_ByCost ().TreeAsString ()}---ENURP---");
					Debug.Assert (_Children.Count >= 2);
					PruneChild (ch);

					Debug.Assert (_Children.Contains (bestLocal));
					Debug.Assert (_Children.Count >= 1);

					anythingChanged = true;
				}
			}
		}

		if (anythingChanged) {
			foreach (var par in TreeParents.ToArray ()) {
				par.Backpropagate ();
			}
		}
	}

	public virtual void PruneChild (AbstractTreeNode child)
	{
		child._Parents.Remove (this);
		var wasRemoved = _Children.Remove (child);
		Debug.Assert (wasRemoved);

		PrunedChildren?.Add (child);
	}

	/// <summary>
	/// Remove bad nodes near the root.
	/// This does NOT guarantee that the best solution candidate survives.
	/// </summary>
	/// 
	/// <param name="ratioToRemove">
	/// Ratio of the eligible children to remove.
	/// Should be > 0 and can be at most 1.
	/// 
	/// Children without playout are, optionally, not part of the eligible set.
	/// One child is also always left out of the set.
	/// </param>
	/// <returns>
	/// Number of removed children
	/// </returns>
	public int MonteCarloPrune (bool pruneNodesWithoutPlayout, double ratioToRemove)
	{
		if (!IsExpanded) {
			throw new InvalidOperationException ("Cannot MC prune without expanding first.");
		}
		if (TreeNodeRequiresAllChildren) {
			throw new InvalidOperationException ("Node requires all child solvabilities, it cannot MC prune.");
		}

		IEnumerable<AbstractTreeNode> chs = _Children;
		if (!pruneNodesWithoutPlayout) {
			chs = chs.Where (ch => ch.MaxCostToFinish < SaturatingCost.Infinity);
		}
		chs = chs
			.OrderBy (ch => ch.LocalCost + ch.MaxCostToFinish)
			.ThenBy (ch => ch.MinCostToFinish)
			.Skip (1)
			.Reverse ()
			.ToArray ();
		var howMany = Math.Max (1, (int) (chs.Count () * ratioToRemove));
		chs = chs.Take (howMany).ToArray ();
		foreach (var ch in chs) {
			PruneChild (ch);
		}
		Debug.Assert (_Children.Count >= 1);

		Backpropagate ();
		ForwardPropagate ();

		return chs.Count ();
	}

	public void ForcePrune (Func<AbstractTreeNode, bool> discard)
	{
		var chs = _Children.ToList ();
		foreach (var ch in chs) {
			if (!discard (ch)) {
				continue;
			}
			PruneChild (ch);
		}
	}

	/// <summary>
	/// Remove all nodes that are not required in a complete solution tree.
	/// Do this recursively, not just at this root node.
	/// This does NOT guarantee that the best solution candidate survives.
	/// </summary>
	public void RecursivePrune ()
	{
		if (!IsExpanded) {
			throw new InvalidOperationException ("Cannot prune without expanding first.");
		}

		if (_Children.Count == 0) {
			return;
		}

		if (TreeNodeRequiresAllChildren) {
			foreach (var ch in _Children) {
				ch.RecursivePrune ();
			}
			return;
		}

		var chs = _Children
			.OrderBy (ch => ch.LocalCost + ch.MaxCostToFinish)
			.ToList ();

		foreach (var ch in chs.Skip (1)) {
			PruneChild (ch);
		}
		Debug.Assert (_Children.Count == 1);

		Backpropagate ();
		ForwardPropagate ();

		_Children.Single ().RecursivePrune ();
	}

	public int GentlePrune ()
	{
		if (!IsExpanded) {
			return 0;
		}
		if (_Children.Count == 0) {
			return 0;
		}
		if (_Children.Count == 1) {
			return _Children [0].GentlePrune ();
		}

		if (TreeNodeRequiresAllChildren) {
			int nRemoved = 0;
			foreach (var ch in _Children) {
				nRemoved += ch.GentlePrune ();
			}
			return nRemoved;
		}

		{
			var nRemoved = MonteCarloPrune (false, ratioToRemove: 0.2);
			//if (DEBUG_MCTS) {
			Console.WriteLine ($"Gently pruned {nRemoved} from {_DebugDisplayString}");
			//}
			return nRemoved;
		}
	}

	private const bool DEBUG_MCTS = 1111 == 111;

	public uint FinalizedPathLenght ()
	{
		if (!IsExpanded) {
			return 0;
		}
		if (_Children.Count == 0) {
			return int.MaxValue;
		}

		if (TreeNodeRequiresAllChildren) {
			return 1 + _Children.Select (c => c.FinalizedPathLenght ()).Min ();
		}
		else {
			if (_Children.Count > 1) {
				return 0;
			}
			return 1 + _Children [0].FinalizedPathLenght ();
		}
	}

	/// <summary>
	/// Due to severe memory constraints, this is a "simplified" version of MCTS.
	/// ... more aptly, a bastard of MCTS and ABS.
	/// It is a bit difficult when a single playout takes 400 GB of RAM.
	/// Restructuring to something more DFS-like would lose other benefits, so I think there is no great solution?
	/// </summary>
	public static (AbstractTreeNode someFrontierNode, uint nNodesCreated) MonteCarloStep (
		AbstractTreeNode root)
	{
		uint nNodesCreated = 0;

		//Console.WriteLine ($"MonteCarloStep {root._DebugName}");
		Debug.Assert (!root.MinCostFromRoot.IsInfinity);
		root.Playouts += 1;

		if (DEBUG_MCTS) {
			Console.WriteLine ($"\nRoot is {root.GetType ().Name}");
			try {
				((dynamic) root).Grid.PrintSelf ();
			}
			catch (Exception ex) {
				// todo
				Thread.Sleep (1000);
			}
		}

		if (!root.IsExpanded) {
			if (DEBUG_MCTS) {
				Console.WriteLine ("Root is NOT expanded yet");
			}
			/*
			 * First time we're seeing this node.
			 * Expand and leaf-parallel rollout
			 */
			root.Expand ();
			root.Backpropagate ();
			root.ForwardPropagate ();

			var chs = root.AllTreeChildren.ToList ();
			if (DEBUG_MCTS) {
				Console.WriteLine ($"Newly expanded root has {chs.Count} children");
			}
			for (int childIndex = 0; childIndex < chs.Count; childIndex++) {
				var ch = chs [childIndex];
				// Note: It may happen that children at root level are pruned during this process
				if (!root.AllTreeChildren.Contains (ch)) {
					continue;
				}

				using var tt = new TimedOperation ($"Fringe RandomPlayout {childIndex + 1}/{chs.Count} in {root._DebugName}", 30);
				// Console.WriteLine ($"Random playout {ch._DebugName}");
				var add = ch.RandomPlayout (false);
				Debug.Assert (add > 0);
				nNodesCreated += add;

				if (!root.TreeNodeRequiresAllChildren && root._Meta.PruneImmediately) {
					root.MonteCarloPrune (pruneNodesWithoutPlayout: false, ratioToRemove: 1);
				}
			}

			if (!root.TreeParents.Any ()) {
				// node was pruned
			}
			else {
				Debug.Assert (root._Children.Count >= 1);
				Debug.Assert (root._IsSolvable.HasValue);
			}

			return (root, nNodesCreated);
		}

		{
			if (DEBUG_MCTS) {
				Console.WriteLine ($"Root has {root._Children.Count} children");
			}
			if (!root._Children.Any ()) {
				Debug.Assert (root._IsSolvable.HasValue);
				return (null, 0);
			}

			if (root.TreeNodeRequiresAllChildren || root._Children.Count <= 1) {
				if (DEBUG_MCTS) {
					Console.WriteLine ($"All children required -OR- only 1 child");
				}
				AbstractTreeNode any = null;
				foreach (var ch in root.AllTreeChildren.ToArray ()) {
					var it = MonteCarloStep (ch);
					any ??= it.someFrontierNode;
					nNodesCreated += it.nNodesCreated;
				}
				return (any, nNodesCreated);
			}

			Debug.Assert (root._Children.Count > 1);
			if (root._Meta.ForciblyPruneAfterVisits >= 0) {
				if (root.Playouts >= (ulong) root._Meta.ForciblyPruneAfterVisits) {
					if (DEBUG_MCTS) {
						Console.WriteLine ($"Root has {root.Playouts} playouts, limit {root._Meta.ForciblyPruneAfterVisits} => pruning");
					}
					root.MonteCarloPrune (pruneNodesWithoutPlayout: false, ratioToRemove: 1);
					Debug.Assert (root._Children.Count >= 1);
				}
			}

			/*
			 * Avoid stepping into completed children.
			 */
			var chs = root._Children
				.Where (ch => !(ch.PruningComplete && (ch.MinCostToFinish == ch.MaxCostToFinish)))
				.ToList ();
			if (DEBUG_MCTS) {
				Console.WriteLine ($"There are {chs.Count} children available for playouts");
			}
			if (chs.Count == 0) {
				// This can happen if Early Pruning is active
				return (null, 0);
			}

			/*
			 * Select a child.
			 * Do not use MaxCostToFinish as criterium, it is initialized as +inf
			 */
			AbstractTreeNode select = null;
			if (AbstractTreeNode.MCTS_weighted_selection) {
				Span<double> weight = stackalloc double [chs.Count];
				Span<double> temp = stackalloc double [chs.Count];

				{
					for (int i = 0; i < chs.Count; ++i) {
						var ch = chs [i];
						var x = ch.Desire;
						Debug.Assert (x >= 0 && x <= 1);
						temp [i] = x;
					}
					for (int i = 0; i < chs.Count; ++i) {
						weight [i] += 1.0 * temp [i];
					}
				}

				{
					var min = double.PositiveInfinity;
					var max = double.NegativeInfinity;
					for (int i = 0; i < chs.Count; ++i) {
						var ch = chs [i];
						var x = Math.Sqrt (ch.Playouts);
						temp [i] = x;
						min = Math.Min (min, x);
						max = Math.Max (max, x);
					}
					for (int i = 0; i < chs.Count; ++i) {
						temp [i] = (temp [i] - min) / (max - min + 0.001);
						Debug.Assert (temp [i] >= 0 && temp [i] <= 1);
					}
					for (int i = 0; i < chs.Count; ++i) {
						weight [i] += 1.0 * (1 - temp [i]);
					}
				}

				{
					var min = ulong.MaxValue;
					var max = ulong.MinValue;
					for (int i = 0; i < chs.Count; ++i) {
						var ch = chs [i];
						var x = ch.MinCostToFinish.AsUlong ();
						temp [i] = x;
						min = Math.Min (min, x);
						max = Math.Max (max, x);
					}
					for (int i = 0; i < chs.Count; ++i) {
						temp [i] = (temp [i] - min) / (max - min + 0.001);
						Debug.Assert (temp [i] >= 0 && temp [i] <= 1);
					}
					for (int i = 0; i < chs.Count; ++i) {
						weight [i] += 1.0 * (1 - temp [i]);
					}
				}

				{
					var r = Util.ThreadRandom.Value;
					var rd = r.NextDouble ();

					for (int i = 0; i < chs.Count - 1; ++i) {
						weight [i + 1] += weight [i];
					}
					rd *= weight [^1];

					for (int i = 0; i < chs.Count; ++i) {
						if (rd <= weight [i]) {
							select = chs [i];
							break;
						}
					}
				}
			}
			else {
				var r = Util.ThreadRandom.Value;
				var rr = r.NextInt64 ();
				double badness (AbstractTreeNode node)
				{
					// This is a rough approximation. It is not true to the original, but it works better in practice.
					var badness = 0.0;
					badness += 1.0 * node.MinCostToFinish.AsUlong ();
					//badness += 1.0 * (1.0 - node.Desire);
					//badness += 1.0 * r.NextDouble ();
					if (rr % 3 == 0) {
						badness -= 2.0 * Math.Sqrt (node.Playouts);
					}

					if (DEBUG_MCTS) {
						if (1111 == 1111) {
							Console.WriteLine ($"{node.TreeParents.First ()._DebugName,8}/{node._DebugName,8}  "
								+ $"{badness,8:0.0} <-- "
								+ $"MinCostToFinish = {node.MinCostToFinish.AsUlong (),5};   "
								+ $"Playouts = {node.Playouts,4};   "
								+ $"1 - Desire = {1.0 - node.Desire,4:0.0}");
						}
					}
					return badness;
				}
				select = chs
					.OrderBy (badness)
					.First ();
			}

			/*
			 * Recurse
			 */
			var ret = MonteCarloStep (select);
			return ret;
		}
	}

	public static bool MCTS_weighted_selection = false;

	public static List<AbstractTreeNode> GetFront (
		AbstractTreeNode root,
		int size)
	{
		var front = new Queue<AbstractTreeNode> ();
		front.Enqueue (root);

		while (front.Count > 0) {
			var pop = front.Dequeue ();
			if (!pop.IsExpanded) {
				pop.Expand ();
				pop.Backpropagate ();
				pop.ForwardPropagate ();
			}

			var chs = pop.AllTreeChildren.ToArray ();
			foreach (var ch in chs) {
				front.Enqueue (ch);
			}

			if (front.Count >= size) {
				break;
			}
		}

		return front.ToList ();
	}

	/// <summary>
	/// Propagate MinCostFromRoot.
	/// Update the best path from the root, if possible.
	/// </summary>
	public void ForwardPropagate ()
	{
		if (MinCostFromRoot == 0) {
			// This is the root
		}
		else {
			var newMinCostFromRoot = SaturatingCost.Infinity;
			foreach (var par in TreeParents) {
				newMinCostFromRoot = SaturatingCost.Min (
					newMinCostFromRoot,
					par.MinCostFromRoot + par.LocalCost);
			}
			MinCostFromRoot = newMinCostFromRoot;
		}

		foreach (var ch in _Children) {
			if (ch.MinCostFromRoot <= MinCostFromRoot + LocalCost + ch.LocalCost) {
				continue;
			}
			ch.ForwardPropagate ();
			Debug.Assert (ch.MinCostFromRoot <= MinCostFromRoot + LocalCost + ch.LocalCost);
		}
	}

	[Obsolete ("Bad performance, keep explicit reference to root instead.")]
	private AbstractTreeNode FindRoot_ByLongestPath ()
	{
		var options = new Queue<(int height, AbstractTreeNode node)> ();
		options.Enqueue ((0, this));
		var best = options.First ();

		while (options.Count > 0) {
			var opt = options.Dequeue ();
			if (opt.node.TreeParents.Count > 0) {
				foreach (var par in opt.node.TreeParents) {
					options.Enqueue ((opt.height + 1, par));
				}
			}
			else {
				if (opt.height > best.height) {
					best = opt;
				}
			}
		}

		return best.node;
	}

	[Obsolete ("Bad performance, keep explicit reference to root instead.")]
	private AbstractTreeNode FindRoot_ByCost ()
	{
		var options = new Queue<AbstractTreeNode> ();
		options.Enqueue (this);
		var best = options.First ();

		while (options.Count > 0) {
			var opt = options.Dequeue ();
			if (opt.TreeParents.Count > 0) {
				foreach (var par in opt.TreeParents) {
					options.Enqueue (par);
				}
			}
			else {
				if (opt.MinCostFromRoot < best.MinCostFromRoot) {
					best = opt;
				}
			}
		}

		return best;
	}

	[Obsolete ("Bad performance, keep explicit reference to root instead.")]
	private List<AbstractTreeNode> FindRootPath_ByCost ()
	{
		var options = new Queue<List<AbstractTreeNode>> ();
		options.Enqueue (new () { this });
		var best = options.First ();

		while (options.Count > 0) {
			var opt = options.Dequeue ();
			if (opt.Last ().TreeParents.Count > 0) {
				foreach (var par in opt.Last ().TreeParents) {
					var add = opt.ToList ();
					add.Add (par);
					options.Enqueue (add);
				}
			}
			else {
				if (opt.Last ().MinCostFromRoot < best.Last ().MinCostFromRoot) {
					best = opt;
				}
			}
		}

		best.Reverse ();
		return best;
	}

	/// <summary>
	/// A short description of this node, concise and suitable for use inside a single line of text.
	/// </summary>
	public virtual string AsStringInline ()
	{
		return ToString ();
	}

	public string TreeAsString (
		int maxDepth = int.MaxValue,
		bool onlyUnprunedNodes = false)
	{
		var sb = new StringBuilder ();
		TreeAsString (sb, new (), new (), maxDepth, onlyUnprunedNodes);
		return sb.ToString ();
	}

	const string _TreeTabChar = "  "; //"\t";

	private void TreeAsString (
		StringBuilder sb,
		List<AbstractTreeNode> stack,
		HashSet<AbstractTreeNode> seen,
		int maxDepth,
		bool onlyUnprunedNodes)
	{
		stack.Add (this);

		if (stack.Count > 1) {
			for (int i = 1; i < stack.Count; i++) {
				sb.Append (_TreeTabChar);
				var at0 = stack [i - 1];
				var at1 = stack [i];
				if (i != stack.Count - 1) {
					if (at1 != at0._Children.Last ()) {
						sb.Append ("|".Color (Color.FromArgb (0x00003040)));
					}
					else {
						sb.Append (" ");
					}
				}
			}

			var col = Color.DarkCyan;
			if (stack [^2]._Children.Count == 1
				|| stack [^2].TreeNodeRequiresAllChildren
				) {
				col = Color.Cyan;
			}
			var tt = stack [^2].TreeNodeRequiresAllChildren
				? $"!- "
				: $"?- ";
			sb.Append (tt.Color (col));

			/*
			var col = Color.White;
			if (Parents.Count == 1 && Parents.Single ().ExpandedChildren.Count + Parents.Single ().UnexpandedChildren.Count == 1) {
				col = Color.Cyan;
			}
			sb.Append ($"P{Parent._DebugId}/A{_DebugId}: {ShortDescription ()}".Color (col));
			sb.Append (" => ");
			if (sb.Length > 1_000_000_000) {
				sb.Append ("[... omitted due to memory ...]\n");
			}
			else {
				Child.Textual (sb, depth + 1);
			}
			*/
		}

		Color fc;
		/*if (!PruningComplete) {
			fc = Color.White;
		}
		else */
		if (IsSolution == true) {
			fc = Color.ForestGreen;
		}
		else if (IsSolution == false) {
			fc = Color.Red;
		}
		else if (IsSolvable == true) {
			fc = Color.GreenYellow;
		}
		else if (IsSolvable == false) {
			fc = Color.Magenta;
		}
		else if (IsSolvable == null) {
			fc = Color.FromArgb (0x00888800);
		}
		else {
			throw new InvalidProgramException ();
		}
		sb.Append ($"{_DebugName}".Color (fc));

		/*
		if (PruningComplete) {
			sb.Append ($"!".Color (Color.Yellow).Background (Color.DarkCyan));
		}
		*/

		sb.Append ($" V = {Playouts}");
		sb.Append ($"; C = {MinCostFromRoot} + {LocalCost} + {MinCostToFinish}:{MaxCostToFinish}");

		if (IsSolution == true) {
			sb.Append ($" ");
			sb.Append ($"[solved]".Color (Color.White).Background (Color.Green));
		}
		else {
			if (!IsExpanded) {
				sb.Append ($", not exp");
			}
			else {
				sb.Append ($", {ExpandedChildren.Count ()}/{_Children.Count} exp");
			}
		}

		sb.Append ($" \"{AsStringInline ()}\"");

		if (!seen.Add (this)) {
			sb.Append ($" [Multiple parents; this is not the primary occurrence]");
			sb.AppendLine ();
		}
		else {
			if (IsExpanded) {
				/*
				if (PrunedNodeCount > 0) {
					sb.Append ('\t', depth);
					sb.Append ($"[{PrunedNodeCount:#,##0} pruned nodes omitted]");
					sb.AppendLine ();
				}
				*/

				if (stack.Count >= maxDepth
					&& ExpandedChildren.Any ()
					) {
					sb.Append ($" [{ExpandedChildren.Count ()} expanded children omitted due depth limit]");
					sb.AppendLine ();
				}
				else if (onlyUnprunedNodes
					&& PruningComplete
					&& (MinCostToFinish == MaxCostToFinish)
					) {
					sb.Append ($" [{ExpandedChildren.Count ()} expanded children omitted as the branch is complete]");
					sb.AppendLine ();
				}
				else if (1111 == 111
					&& sb.Length > 8000
					&& ExpandedChildren.Any ()
					) {
					sb.Append ($" [{ExpandedChildren.Count ()} expanded children omitted due to memory]");
					sb.AppendLine ();
				}
				else {
					sb.AppendLine ();
					foreach (var ch in ExpandedChildren) {
						ch.TreeAsString (sb, stack, seen, maxDepth, onlyUnprunedNodes);
					}
				}

				foreach (var ch in _Children.Where (ch => !ch.IsExpanded)) {
					ch.TreeAsString (sb, stack, seen, maxDepth, onlyUnprunedNodes);
				}
			}
			else {
				sb.AppendLine ();
			}
		}

		stack.RemoveAt (stack.Count - 1);
	}

	public override int GetHashCode ()
	{
		var hash = new HashCode ();
		hash.Add (_DebugId);
		return hash.ToHashCode ();
	}

	public long CountTreeNodes ()
	{
		return 1L + AllTreeChildren.Sum (c => c.CountTreeNodes ());
	}
}
