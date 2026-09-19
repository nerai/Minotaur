using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using Minotaur.Boards.Common;
using Minotaur.GenericTreeSearch;
using Minotaur.Utils;

namespace Minotaur.Boards.Strings;

public sealed class WormyNode : TreeNode<WormyBoardGrid>
{
	protected WormyTreeCreationMeta Meta => (WormyTreeCreationMeta) _Meta;

	private ImmutableArray<string> UsedPieces;

	private ImmutableArray<Worm> BadClosedSubspace = ImmutableArray<Worm>.Empty;

	private ImmutableArray<Worm> BadOpenSubspace_Restrictor = ImmutableArray<Worm>.Empty;

	private ImmutableArray<Worm> BadOpenSubspace_Reachable = ImmutableArray<Worm>.Empty;

	private ImmutableArray<Worm> BadSymmetricSubspace = ImmutableArray<Worm>.Empty;

	private Worm DuplicatePiece = null;

	private Piece NoPlaceForPiece = null;

	private List<(Piece p, ImmutableArray<Worm> sourceWorms)> _PiecePlacementOptions = null;

	public int? AppliedPivotLimit { get; private set; } = null;
	public int? PossiblePivots { get; private set; } = null;


	public WormyNode (
		WormyTreeCreationMeta meta,
		WormyBoardGrid grid)
		: base (meta, grid)
	{
		/*
		 * Note: In the ctor, this node is not yet added to the pool of known nodes.
		 * So do NOT do anything in here that affects other nodes in any way.
		 */
	}

	/// <summary>
	/// Return true iff no children should be created for this node.
	/// This is the case when either
	/// a) Board solved
	/// b) Board cannot be solved
	/// </summary>
	private bool CheckEndCondition ()
	{
		/*
		 * Merges should not create worms with > 5 cells.
		 * Check that all worms have <= 5 cells.
		 */
		if (Grid.AllWorms.Any (w => w.Cells.Length > 5)) {
			throw new InvalidProgramException ("Worms were assumed to never acquire more than 5 cells - some logic error happened.");
		}

		/*
		 * Is there a piece duplication?
		 */
		/*
		 * TODO XXX diese liste speichern. das ist wichtig für WORM_PIVOT_PIECE
		 * einerseits damit es nicht doppelt gemacht wird (ist aber nur performance)
		 * andererseits aber auch damit man erkennen kann ob es keine pieces mehr gibt die die restlichen cells auffuellen koennen!
		 */
		{
			using var UsedPiecesBuffer = new SmallSet<string> (12);
			foreach (var w in Grid.AllWorms) {
				if (w.Cells.Length < 5) {
					continue;
				}

				var hash = w.Cells.GetFillHashOfPiece (Grid.SX, Grid.SY);
				var piece = Piece.PieceHashMap [hash];
				if (!UsedPiecesBuffer.Add (piece.Name)) {
					Info.Add (new UnsolvabilityInfo (
						UnsolvabilityInfo.UnsolvabilityReason.PieceDuplication,
						piece.Name,
						$"Duplicate piece {piece.Name}",
						$"in {w.ToShortString ()}"));
					DuplicatePiece = w;
					SetIsSolution (false);
					return true;
				}
			}
			UsedPieces = UsedPiecesBuffer.ToImmutableArray ();
		}

		/*
		 * Only in case of 12 pieces in use:
		 * Is there a piece that does not fit on the board anymore?
		 * (TODO: for <12 pieces this is possible if a piece was excluded earlier)
		 */
		if (Grid.TotalOpenCellCount >= 60) {
			_PiecePlacementOptions = new List<(Piece p, ImmutableArray<Worm> sourceWorms)> ();

			foreach (var piece in Piece.AllPieces) {
				if (UsedPieces.Contains (piece.Name)) {
					continue;
				}

				var options = WormyPivot_Piece
					.FindInsertionPoints (Grid, piece)
					.ToList ();
				if (options.Count == 0) {
					Info.Add (new UnsolvabilityInfo (
						UnsolvabilityInfo.UnsolvabilityReason.CannotKeepDancing,
						piece.Name,
						$"There is no place for piece {piece.Name}",
						""));
					NoPlaceForPiece = piece;
					SetIsSolution (false);
					return true;
				}

				_PiecePlacementOptions.AddRange (options);
			}
		}

		/*
		 * Are there any incomplete worms?
		 * If not, this is a solution.
		 */
		var incompleteWorms = Grid.AllWorms.Where (w => w.Cells.Length < 5)
			.ToList ();
		if (incompleteWorms.Count == 0) {
			SetIsSolution (true);
			return true;
		}

		/*
		 * Is there an incomplete worm with only disallowed edges?
		 */
		foreach (var w in incompleteWorms) {
			if (w.NeighbourWorms_OnlyAllowed ().Count == 0) {
				Info.Add (new UnsolvabilityInfo (
					UnsolvabilityInfo.UnsolvabilityReason.CannotCompleteString,
					w.ToShortString (),
					$"No continuation for string",
					$"{w}"));
				SetIsSolution (false);
				return true;
			}
		}

		/*
		 * Subspace divisibility
		 */
		if (!CheckSubspaceDivisibility ()) {
			SetIsSolution (false);
			return true;
		}

		/*
		 * Check for violations of incomplete subspace divisibility.
		 * This is applied for vertex cuts of size 1, 2 or 3.
		 */
		if (!CheckIncompleteSubspaceDivisibility (incompleteWorms)) {
			SetIsSolution (false);
			return true;
		}

		/*
		 * Subspace symmetry
		 */
		if (!CheckSubspaceSymmetry ()) {
			SetIsSolution (false);
			return true;
		}

		return false;
	}

	public override string TextualRepresentation (BoardGrid.TextualRepresentationStyle style, List<int> highlight = null)
	{
		var s = base.TextualRepresentation (style, highlight);
		var sPieces = string.Join (",", UsedPieces);
		s += $"$({sPieces})\n";
		return s;
	}

	public bool IsInitialitzingNearRoot ()
	{
		if (MinCostFromRoot == 0) {
			Debug.Assert (!Parents.Any ());
			return true;
		}
		foreach (var parAct in Parents) {
			if (false
				|| parAct is WormyAction_Boardsplit
				|| parAct is WormyAction_Bottleneck
				) {
				var wormNode = (WormyNode) parAct.Parent.Parent;
				if (wormNode == null) {
					// This path was pruned, ignore it
					continue;
				}
				if (wormNode.IsInitialitzingNearRoot ()) {
					return true;
				}
			}
		}
		return false;
	}

	~WormyNode ()
	{
		// This is required in addition to explicit pruning, because those calls do not account for other nodes rooted in a pruned node
		var meta = (WormyTreeCreationMeta) _Meta;
		meta.Pool.ReturnNode (this);
	}

	protected override void CreateChildren ()
	{
		Debug.Assert (!IsExpanded);

		if (Meta.checkEndCondition) {
			if (CheckEndCondition ()) {
				Debug.Assert (IsSolution.HasValue);
				return;
			}
		}

		var incompleteWorms = Grid.AllWorms
			.Where (w => w.Cells.Length < 5)
			.ToList ();
		if (Meta.checkEndCondition) {
			// Assume all incompletes have neighbour options - else this node would have been declared a dead end in the ctor already
			Debug.Assert (incompleteWorms.All (w => w.NeighbourWorms_OnlyAllowed ().Count > 0));
		}

		var childrenToAdd = new List<WormyPivot> ();

		/*
		 * Check if there is a mandatory space to be added to some worm.
		 * For this purpose, detect a vertex separation using a cutting edge of
		 * size 1 (i.e. all expansions to >= 5 nodes require adding a certain node).
		 * 
		 * This also solves the common 2x2 pattern, which has to be entirely
		 * connected and would lead to unnecessary leaf nodes if not caught early.
		 */
		if (Meta.allowBottlenecks) {
			childrenToAdd.AddRange (CreateChildrenForBottleneck (incompleteWorms));
		}

		/*
		 * Board split
		 */
		if (Meta.allowBoardsplit) {
			childrenToAdd.AddRange (CreateChildrenForBoardsplit (incompleteWorms));
		}

		/*
		 * Choice
		 */
		if (Meta.allowChoices) {
			childrenToAdd.AddRange (CreateChildrenForChoice (incompleteWorms));
		}

		/*
		 * Select a pair of nodes and decide if they are connected
		 */
		if (Meta.allowDecisions) {
			childrenToAdd.AddRange (CreateChildrenForDecision (incompleteWorms));
		}

		/*
		 * Remove superfluous children.
		 * Near the root, do not switch between various actions - just do everything in some arbitrarily fixed order.
		 * 
		 * NOTE: This could cause a child to be removed that is good (even BestPivot).
		 * This can happen if a child gets updated before this method completed.
		 * This is then a bug and should be fixed or avoided.
		 */
		var isInitialitzingNearRoot = IsInitialitzingNearRoot ();
		if (
			//isInitialitzingNearRoot &&
			childrenToAdd.Any (c => c is WormyPivot_Bottleneck || c is WormyPivot_Boardsplit)
			) {
			/*
			 * If we are still initializing, i.e. creating the worms that are forced already
			 * on the source board, then never create anything else.
			 * This optimization avoids stupid deviations early in the tree.
			 */
			PossiblePivots = childrenToAdd.Count;
			AppliedPivotLimit = 1;

			childrenToAdd.RemoveAll (piv => !(piv is WormyPivot_Bottleneck) && !(piv is WormyPivot_Boardsplit));
			if (childrenToAdd.Count > 1) {
				childrenToAdd.Sort ();
				childrenToAdd.RemoveRange (1, childrenToAdd.Count - 1);
			}
			Debug.Assert (childrenToAdd.Count >= 1);
		}
		else {
			if (Meta.allowPieceOperation) {
				/*
				 * Try to place the X and I pieces directly. They often greatly limit the options available.
				 * 
				 * For now, only do this near the root node.
				 */
				if (Grid.TotalOpenCellCount == 60) {
					// Don't do this if there are very few pieces remaining
					if (Grid.UnfinishedCells >= 60 / 2) {
						childrenToAdd.AddRange (CreateChildrenForDirectPiece (incompleteWorms, Meta));
					}
				}
			}

			PossiblePivots = childrenToAdd.Count;
			AppliedPivotLimit = int.MaxValue;
		}

		if (childrenToAdd.Count == 0) {
			// This is not supposed to happen
			Grid.PrintSelf ();
			Debugger.Break ();
		}

		foreach (var child in childrenToAdd) {
			AddTreeChild (child);
		}
		ForwardPropagate ();
	}

	private IEnumerable<WormyPivot> CreateChildrenForBottleneck (
		IReadOnlyCollection<Worm> incompleteWorms)
	{
		// todo kann man das nicht vereinigen mit der ISSE methode?
		var options = new List<(Worm pivot, Worm cut, ImmutableArray<Worm> reachable, int total)> ();

		foreach (var pivot in incompleteWorms) {
			foreach (var (cut, reachable) in FindForcedSmallGroupings (pivot, Grid)) {
				options.Add ((pivot, cut, reachable, reachable.Sum (w => w.Cells.Length)));
			}
		}

		if (options.Count == 0) {
			yield break;
		}

		options = options
			.OrderByDescending (r => r.total)
			.ThenByDescending (r => r.reachable.Length)
			.ToList ();
		var best = options.First ();
		foreach (var option in options) {
			double desire = Meta.BaseDesireForBottleneck * (1 + option.total / (best.total + 0.001));
			var piv = new WormyPivot_Bottleneck (
				this,
				Meta,
				option,
				desire);
			yield return piv;
		}
	}

	public IEnumerable<(Worm restrict, ImmutableArray<Worm> reachable)> FindForcedSmallGroupings (
		Worm pivot,
		WormyBoardGrid grid)
	{
		using var reachedWorms = new SmallSet<Worm> (5) { pivot };
		var nReachedCells = pivot.Cells.Length;

		using var open = pivot.NeighbourWorms_OnlyAllowed ();

		while (open.Count > 0) {
			/*
			 * Is the fringe a chockepoint when the main body EXcludes the cut vertex?
			 */
			Debug.Assert (nReachedCells <= 5);
			if (reachedWorms.Count > 1 && open.Count == 1) {
				yield return (open.Single (), reachedWorms.AsImmutable ());
			}

			/*
			 * Add the fringe to the main body.
			 * Check if the fringe contains a disallowed node, or is in conflict with itself.
			 * If so, then it is impossible to continue here - a decision has to be made elsewhere.
			 */
			foreach (var o in open) {
				if (reachedWorms.Any (a => a.IsNotAllowedToConnectTo (o))) {
					yield break;
				}

				/*
				 * Did the set of cells become too large already?
				 */
				nReachedCells += o.Cells.Length;
				if (nReachedCells > 5) {
					yield break;
				}

				reachedWorms.Add (o);
			}

			/*
			 * Is the fringe a chockepoint when the main body INcludes the cut vertex?
			 */
			Debug.Assert (reachedWorms.Count > 1);
			if (open.Count == 1) {
				yield return (open.Single (), reachedWorms.AsImmutable ());
			}

			/*
			 * Determine the next fringe set.
			 */
			open.ReplaceMany (o => o._Neighbors);
			open.RemoveWhere (reachedWorms.Contains);
		}
	}

	private IEnumerable<WormyPivot> CreateChildrenForBoardsplit (
		IReadOnlyCollection<Worm> incompleteWorms)
	{
		var allComponents = WormyBoardGrid.BuildConnectedComponents (Grid, incompleteWorms, false);
		var options = new List<(Worm restrict, ImmutableArray<Worm> set)> ();

		/*
		 * First, split all open space into connected compontents.
		 * Then search each CC individually for a restrictor string that splits it.
		 * This avoids false positives that would occur if the 'restrictor' detected a split that was already present before.
		 */
		foreach (var area in allComponents) {
			/*
			 * Search this CC for a separator.
			 */
			foreach (var restrict in area) {
				using var open = new SmallSet<Worm> (area);
				open.Remove (restrict);
				var components = WormyBoardGrid.BuildConnectedComponents (Grid, open, false);
				if (components.Count <= 1) {
					continue;
				}

				/*
				 * We found a separator.
				 * Check if any of the components (there are 2 or more) has size 0 mod 5.
				 */
				bool restrictorInUse = false;
				foreach (var component in components) {
					var sumInnerCells = component.Sum (w => w.Cells.Length);

					if (sumInnerCells % 5 == 0) {
						/*
						 * Works as-is
						 */
					}
					else if (!restrictorInUse && (sumInnerCells + restrict.Cells.Length) % 5 == 0) {
						/*
						 * This component becomes valid if it is amended by the restrictor string
						 * Since the restrictor is used here, it cannot be used elsewhere without violating divisibility of this component.
						 */
						component.Add (restrict);
						restrictorInUse = true;
					}
					else {
						continue;
					}

					options.Add ((restrict, component.AsImmutable ()));
				}
			}

			area.Dispose ();
		}

		if (options.Count == 0) {
			yield break;
		}

		/*
		 * Picking any option is fine - others will be derived in the next step.
		 * Just because it looks nicer, prefer small options.
		 */
		const double BaseDesireForBoardsplit = 800;
		var tup = options
			.OrderBy (o => o.set.Length)
			.First ();
		var piv = new WormyPivot_Boardsplit (
			this,
			Meta,
			tup,
			BaseDesireForBoardsplit);
		yield return piv;
	}

	private IEnumerable<WormyPivot> CreateChildrenForDirectPiece (
		IReadOnlyCollection<Worm> incompleteWorms,
		WormyTreeCreationMeta meta)
	{
		if (_PiecePlacementOptions.Count == 0) {
			yield break;
		}

		var groupedOptions = _PiecePlacementOptions
			.GroupBy (opt => opt.p)
			.OrderBy (g => g.Count ());
		var best = groupedOptions.First ();
		Debug.Assert (best.Count () >= 1);

		/*
		 * im moment wird das nur bei initial(!) 12 pieces gemacht. sonst nie.
		 * daher kann man JETZT NOCH davon ausgehen dass es immer eine loesung gibt etc.
		 * man kann also einfach das piv nehmen mit den wenigsten optionen.
		 */

		foreach (var option in groupedOptions) {
			var piv = new WormyPivot_Piece (
				this,
				meta,
				option.Key,
				option);
			var n = piv.ChildActions.Count ();
			Debug.Assert (n >= 1);

			// todo normalize inputs
			var desire = Meta.Desire_Piece_Base
				+ Meta.Desire_Piece_Range * (best.Count () / (n + 0.001));
			desire += (60 - Grid.UnfinishedCells) * Meta.Desire_Piece_Cells;
			desire += n * n * Meta.Desire_Piece_Options;

			piv.Desire = desire;
			yield return piv;
		}
	}

#if false
	static int _Debug_DesireCalls = 0;
	static Dictionary<string, (int n, double val, double impactabs, double impact)> _Debug_OperationHistogram = new ();

	private static void Desire (
		ref double desire,
		double weight,
		double value,
		string name)
	{
		desire += weight * value;

		lock (_Debug_OperationHistogram) {
			_Debug_OperationHistogram.TryGetValue (name, out var old);
			_Debug_OperationHistogram [name] = (
				old.n + 1,
				(old.val * old.n + Math.Abs (value)) / (old.n + 1),
				(old.impactabs * old.n + Math.Abs (weight * value)) / (old.n + 1),
				(old.impact * old.n + (weight * value)) / (old.n + 1)
				);
			if (++_Debug_DesireCalls % 5_000_000 == 0) {
				Console.WriteLine ($"\n{_Debug_DesireCalls}:");
				var avgV = _Debug_OperationHistogram.Values.Average (pair => pair.val);
				var avgIa = _Debug_OperationHistogram.Values.Average (pair => pair.impactabs);
				var avgIs = _Debug_OperationHistogram.Values.Average (pair => pair.impact);

				var pairs = _Debug_OperationHistogram
					.OrderByDescending (p => p.Value.impactabs);
				foreach (var pair in pairs) {
					var v = pair.Value;
					Console.WriteLine (
						$"{v.n,10}x, " +
						$"val {v.val,5:0.00}/{v.val / avgV,4:0.00}, " +
						$"absimpact {v.impactabs,8:0.0}, " +
						$"impact {v.impact,8:0.0}" +
						$"  {pair.Key}");
				}
			}
		}
	}
#else
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	private static void Desire (
		ref double desire,
		double weight,
		double value,
		string name)
	{
		desire += weight * value;
		name = null; // Keep this argument, it is required for normalization
	}
#endif

	private IEnumerable<WormyPivot> CreateChildrenForChoice (
		IReadOnlyCollection<Worm> incompleteWorms)
	{
		var options = incompleteWorms
			.Select (pivot => {
				var nbs = pivot.NeighbourWorms_OnlyAllowed ();

				/*
				 * Can we connect to more than one neighbour?
				 * Look at the cell count of the two smallest neighbours.
				 */
				var smallestTwoNbsCellCount = nbs
					.OrderBy (nb => nb.Cells.Length)
					.Take (2)
					.Sum (nb => nb.Cells.Length);
				if (pivot.Cells.Length + smallestTwoNbsCellCount <= 5) {
					/*
					 * Multiple connections are possible.
					 * We cannot perform an "exclusive choice" operation.
					 */
					return null;
				}

				/*
				 * We have to pick exactly one neighbour.
				 * All of them are valid options (total cell count <= 5).
				 */
				var desire = 0.0;

				/*
				 * Hotness: spatial locality is good
				 */
				Desire (ref desire, Meta.Desire_Choice_P_Ht, 15 * Hotness (pivot), nameof (Meta.Desire_Choice_P_Ht));

				// Many cells in pivot = very good
				Desire (ref desire, Meta.Desire_Choice_P0_C, 3 * pivot.Cells.Length, nameof (Meta.Desire_Choice_P0_C));

				// Many options for this pivot = bad
				// Big enighbors = good
				var sumNbsCells = nbs.Sum (nb => nb.Cells.Length);
				Desire (ref desire, Meta.Desire_Choice_P1_S, 3 * nbs.Count, nameof (Meta.Desire_Choice_P1_S));
				Desire (ref desire, Meta.Desire_Choice_P1_C, 2 * sumNbsCells, nameof (Meta.Desire_Choice_P1_C));
				Desire (ref desire, Meta.Desire_Choice_P1_A, 3 * sumNbsCells / nbs.Count, nameof (Meta.Desire_Choice_P1_A));

				if (Meta.Desire_IncludeReachable2) {
					var r = pivot.CountReachableAllowedInDistance (2);
					Desire (ref desire, Meta.Desire_Choice_P2_S, 2 * r.worms, nameof (Meta.Desire_Choice_P2_S));
					Desire (ref desire, Meta.Desire_Choice_P2_C, r.cells, nameof (Meta.Desire_Choice_P2_C));
					Desire (ref desire, Meta.Desire_Choice_P2_A, 3 * r.cells / r.worms, nameof (Meta.Desire_Choice_P2_A));
				}

				Desire (ref desire, Meta.Desire_Choice_Base, 5 * 1, nameof (Meta.Desire_Choice_Base));

				return new {
					Pivot = pivot,
					Nbs = nbs,
					Desire = desire,
				};
			})
			.Where (option => option != null)
			.OrderByDescending (p => p.Desire)
			.ToList ();
		if (options.Count == 0) {
			yield break;
		}

		//var best = options.First ().Desire;
		//var worst = options.Last ().Desire - 0.001;
		foreach (var option in options) {
			var nbs = option.Nbs;
			// If this is <= 1, then the connection is forced anyway, and a mincut should be used instead
			if (nbs.Count <= 1) {
				continue;
			}

			//var norm = (option.Desire - worst) / (best - worst);
			//double desire = Meta.Desire_Choice_Base * (1 + norm);
			double desire = option.Desire;

			var piv = new WormyPivot_Choice (
				this,
				Meta,
				option.Pivot,
				option.Nbs,
				desire);
			yield return piv;
		}
	}

	private IEnumerable<WormyPivot> CreateChildrenForDecision (
		IReadOnlyCollection<Worm> incompleteWorms)
	{
		/*
		 * Cache all valid neighbors for all open strings
		 */
		var allNbs = incompleteWorms.ToDictionary (w => w, w => {
			var nbs = w.NeighbourWorms_OnlyAllowed ();
			return nbs;
		});

		IEnumerable<(Worm Pivot, Worm Nb, double Desire)> CreateOptionsForPivot (Worm pivot)
		{
			var pivNbs = allNbs [pivot].ToList ();

			// If this is <= 1, then the connection is forced anyway, and a mincut should be used instead
			if (Meta.prefilterDecisions && pivNbs.Count <= 1) {
				yield break;
			}

			foreach (var nb in pivNbs) {
				Debug.Assert (nb != pivot);
				if (nb.Cells.Min () < pivot.Cells.Min ()) {
					// These are equivalent, pick only one
					continue;
				}

				var desire = 0.0;
				var __P = pivot.Cells.Length >= nb.Cells.Length ? pivot : nb;
				var __N = pivot.Cells.Length >= nb.Cells.Length ? nb : pivot;

				/*
				 * Hotness: spatial locality is good
				 */
				Desire (ref desire, Meta.Desire_Decision_P_Ht, 10 * Hotness (__P), nameof (Meta.Desire_Decision_P_Ht));
				Desire (ref desire, Meta.Desire_Decision_N_Ht, 200 * Hotness (__N), nameof (Meta.Desire_Decision_N_Ht));

				/*
				 * Many cells in sources = slightly good.
				 * However, increase it because this makes it continue from a previous merge (for spatial locality).
				 */
				Desire (ref desire, Meta.Desire_Decision_P0_C, 5 * __P.Cells.Length, nameof (Meta.Desire_Decision_P0_C));
				Desire (ref desire, Meta.Desire_Decision_N0_C, 5 * __N.Cells.Length, nameof (Meta.Desire_Decision_N0_C));

				/*
				 * Many options for the sources = very bad.
				 * This has less impact than it should because of the above reason (spatial locality).
				 */
				Desire (ref desire, Meta.Desire_Decision_P1_S, 2 * allNbs [__P].Count, nameof (Meta.Desire_Decision_P1_S));
				Desire (ref desire, Meta.Desire_Decision_N1_S, 2 * allNbs [__N].Count, nameof (Meta.Desire_Decision_N1_S));
				var c0 = allNbs [__P].Sum (w => w.Cells.Length);
				var c1 = allNbs [__N].Sum (w => w.Cells.Length);
				Desire (ref desire, Meta.Desire_Decision_P1_C, 2 * c0, nameof (Meta.Desire_Decision_P1_C));
				Desire (ref desire, Meta.Desire_Decision_N1_C, 2 * c1, nameof (Meta.Desire_Decision_N1_C));
				Desire (ref desire, Meta.Desire_Decision_P1_A, 5 * c0 / allNbs [__P].Count, nameof (Meta.Desire_Decision_P1_A));
				Desire (ref desire, Meta.Desire_Decision_N1_A, 5 * c1 / allNbs [__N].Count, nameof (Meta.Desire_Decision_N1_A));

				if (Meta.Desire_IncludeReachable2) {
					var r0 = __P.CountReachableAllowedInDistance (2);
					var r1 = __P.CountReachableAllowedInDistance (2);

					Desire (ref desire, Meta.Desire_Decision_P2_S, 1 * r0.worms, nameof (Meta.Desire_Decision_P2_S));
					Desire (ref desire, Meta.Desire_Decision_N2_S, 1 * r1.worms, nameof (Meta.Desire_Decision_N2_S));
					Desire (ref desire, Meta.Desire_Decision_P2_C, 1 * r0.cells, nameof (Meta.Desire_Decision_P2_C));
					Desire (ref desire, Meta.Desire_Decision_N2_C, 1 * r1.cells, nameof (Meta.Desire_Decision_N2_C));
					Desire (ref desire, Meta.Desire_Decision_P2_A, 6 * r0.worms / r0.cells, nameof (Meta.Desire_Decision_P2_A));
					Desire (ref desire, Meta.Desire_Decision_N2_A, 6 * r1.worms / r1.cells, nameof (Meta.Desire_Decision_N2_A));
				}

				Desire (ref desire, Meta.Desire_Decision_Base, 5 * 1, nameof (Meta.Desire_Decision_Base));

				yield return (
					Pivot: pivot,
					Nb: nb,
					Desire: desire
				);
			}
		}

		var options = incompleteWorms
			.SelectMany (CreateOptionsForPivot)
			.OrderByDescending (p => p.Desire)
			.ToList ();
		if (options.Count == 0) {
			/*
			 * This is allowed now because mincuts are not mandatory to use anymore
			var bug = incompleteWorms.FirstOrDefault (w => allNbs [w].Count > 0);
			if (bug != null) {
				throw new InvalidProgramException ("No options for merge but there should be some.");
			}
			*/
			yield break;
		}

		/*
		 * If there are already several options, ignore further options - the tree would grow too vast.
		 * The further down we are in the tree, the more restrictive we become.
		 * Note that this limit is currently ridiculously high and will only come to play in cases which
		 * are either rather extreme (symmetric early game with many 2-wide tubes) or very simple (endgame).
		 */
		var remainingCells = incompleteWorms.Sum (w => w.Cells.Length);
		if (Meta.prefilterDecisions) {
			options = options
				.Take (remainingCells / 2)
				.ToList ();
		}

		//var best = options.First ().Desire;
		//var worst = options.Last ().Desire - 0.001;
		foreach (var option in options) {
			//var norm = (option.Desire - worst) / (best - worst);
			//double desire = Meta.Desire_Decision_Base * (1 + norm);
			double desire = option.Desire;

			Worm neighbour = option.Nb;
			var piv = new WormyPivot_Decision (
				this,
				Meta,
				option.Pivot,
				option.Nb,
				desire);
			yield return piv;
		}
	}

	private bool CheckSubspaceDivisibility ()
	{
		var spaces = Grid.Subspaces ();
		var bad = spaces
			.Where (space => space.Sum (w => w.Cells.Length) % 5 != 0)
			.OrderBy (space => space.Count)
			.FirstOrDefault ();
		if (bad == null) {
			return true;
		}

		BadClosedSubspace = bad.AsImmutable ();
		var s = string.Join (",", BadClosedSubspace.Select (w => w.ToShortString ()));
		Info.Add (new UnsolvabilityInfo (
			UnsolvabilityInfo.UnsolvabilityReason.CannotCompleteString,
			s,
			$"Closed subspace divisibility",
			$"in {s}"));

		return false;
	}

	private bool CheckIncompleteSubspaceDivisibility (List<Worm> incompleteWorms)
	{
		var options = new List<(Worm [] restrictor, SmallSet<Worm> reachable)> ();

		foreach (var restrict1 in incompleteWorms) {
			foreach (var restrict2 in incompleteWorms) {
				/*
				 * Some notes:
				 * 
				 * restrict1 and 2 may be the same.
				 * This denotes the case that we only cover one cut. It is easiest to combine it in one larger loop.
				 * 
				 * restrict1 and 2 may or may not be neighbours.
				 * Either condition is possible, neither can be ruled out.
				 */

				/*
				 * What is the capacity of the separating vertices?
				 * If this value is >= 4, their capacity is too large and we cannot deduce anything.
				 */
				var sumRestrictingCapacity = 5 - restrict1.Cells.Length;
				if (restrict1 != restrict2) {
					sumRestrictingCapacity += 5 - restrict2.Cells.Length;
				}
				if (sumRestrictingCapacity >= 4) {
					continue;
				}

				var open = new SmallSet<Worm> (incompleteWorms);
				open.Remove (restrict1);
				open.Remove (restrict2);
				var components = WormyBoardGrid.BuildConnectedComponents (
					Grid,
					open,
					true // TODO: muss das wirklich true sein?  xxx
					);
				if (components.Count <= 1) {
					continue;
				}

				/*
				 * Check each connected component for validiy
				 */
				foreach (var component in components) {
					var sumInnerCells = component.Sum (w => w.Cells.Length);

					if (sumInnerCells % 5 > sumRestrictingCapacity) {
						/* 
						 * It is not possible to connect the worms in a legal way.
						 * The capacity of the restricting string is incompatible with the contained string's missing tile count.
						 */
						var restricts = restrict1 != restrict2
							? new [] { restrict1, restrict2 }
							: new [] { restrict1 };
						options.Add ((restricts, component));
					}
				}
			}
		}

		if (options.Count == 0) {
			// todo fuer 3 restricts
		}

		if (options.Count == 0) {
			return true;
		}

		// Select easiest option
		// Note: options use least amount of cutting worms already, no need to sort for that.
		var best = options
			.OrderBy (o => o.reachable.Count)
			.First ();

		BadOpenSubspace_Restrictor = best.restrictor.ToImmutableArray ();
		BadOpenSubspace_Reachable = best.reachable.AsImmutable ();

		/*
		Console.WriteLine ();
		GridPrintSelf ();
		Console.WriteLine (string.Join (",", BadOpenSubspace_Restrictor.Select (r => r.ToShortString ())));
		Console.WriteLine (string.Join (",", BadOpenSubspace_Reachable.Select (w => w.ToShortString ())));
		Console.WriteLine ($"sumRestrictingNodeCapacity = {sumRestrictingNodeCapacity}");
		Console.WriteLine ($"total = {total}");
		Console.WriteLine ($"sumInnerCells = {sumInnerCells}");
		Console.WriteLine ();
		*/
		var s1 = string.Join (",", BadOpenSubspace_Restrictor.Select (w => w.ToShortString ()));
		var s2 = string.Join (",", BadOpenSubspace_Reachable.Select (w => w.ToShortString ()));
		Info.Add (new UnsolvabilityInfo (
			UnsolvabilityInfo.UnsolvabilityReason.CannotCompleteString,
			s2,
			$"Open subspace divisibility",
			$"induced by {s1}: {s2}"));

		return false;
	}

	private bool CheckSubspaceSymmetry ()
	{
		var subspaces = Grid
			.Subspaces ()
			.Where (space => space.Sum (w => w.Cells.Length) == 10)
			.ToList ();
		foreach (var subspace in subspaces) {
			var isSymmetric = AreCellsPointSymmetric (subspace);
			if (!isSymmetric) {
				continue;
			}

			BadSymmetricSubspace = subspace.AsImmutable ();

			var s = string.Join (",", BadSymmetricSubspace.Select (w => w.ToShortString ()));
			Info.Add (new UnsolvabilityInfo (
				UnsolvabilityInfo.UnsolvabilityReason.CannotCompleteString,
				s,
				$"Symmetric 10 cell subspace",
				$"in {s}"));
			return false;
		}

		return true;
	}

	private bool AreCellsPointSymmetric (SmallSet<Worm> subspace)
	{
		var SX = Grid.SX;
		var SY = Grid.SY;

		var coords = subspace
			.SelectMany (w => w.Cells)
			.Select (c => (c % SX, c / SX))
			.ToList ();
		var minx = coords.Min (c => c.Item1);
		var miny = coords.Min (c => c.Item2);
		var maxx = coords.Max (c => c.Item1);
		var maxy = coords.Max (c => c.Item2);

		foreach (var c in coords) {
			var x = c.Item1;
			var y = c.Item2;
			// Mirror on the center point, via a' = 2 * center - a, and 2 * center = min + max
			x = minx + maxx - x;
			y = miny + maxy - y;
			if (!coords.Contains ((x, y))) {
				return false;
			}
		}

		return true;
	}

	public override string AsStringInline ()
	{
		var sb = new StringBuilder ();

		sb.Append ($"Board: ");

		if (IsSolution == true) {
			sb.Append ("solution");
		}
		else if (IsSolution == false) {
			sb.Append ("dead end");
		}
		else if (IsSolvable == true) {
			sb.Append ("solvable");
		}
		else if (IsSolvable == true) {
			sb.Append ("not solvable");
		}
		else {
			sb.Append ("solvability unknown");
		}

		if (UsedPieces != null && UsedPieces.Length > 0) {
			sb.Append ($" Using pieces ");
			sb.Append (string.Join (", ", UsedPieces));
		}

		return sb.ToString ();
	}

	public int Hotness (Worm worm)
	{
		var min = worm.Cells.Min ();
		foreach (var act in Parents.Cast<WormyAction> ()) {
			foreach (var w in act.HotWorms ()) {
				if (w.Cells.Min () == min) {
					return 1;
				}
			}
		}

		return 0;
	}
}
