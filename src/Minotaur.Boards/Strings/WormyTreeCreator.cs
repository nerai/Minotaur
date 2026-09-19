using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Minotaur.Boards.Common;
using Minotaur.GenericTreeSearch;
using Minotaur.Utils;

namespace Minotaur.Boards.Strings;

public class WormyTreeCreator
{
	public bool PrintToConsole = true;
	public bool UseMCTS;
	public string Logfile = null;
	public List<ulong> PrunedAtPlayouts = new ();

	public WormyTreeCreator (
		bool useMCTS)
	{
		UseMCTS = useMCTS;
	}

	/// <summary>
	/// Remember to provide a new Meta - do not reuse an old one, its node pool is tainted!
	/// </summary>
	public WormyNode CreateTree (
		string hash,
		ulong maxIterations,
		WormyTreeCreationMeta meta)
	{
		if (meta.Pool.PoolIsInUse) {
			throw new ArgumentException ("Do not re-use a Meta instance!");
		}

		if (PrintToConsole) {
			lock (Console.Out) {
				Console.WriteLine ($"Creating tree {hash}");
			}
		}

		if (1111 == 111) {
			Logfile = $"worm tree logs/{hash}.txt";
		}

		using var to = new TimedOperation ($"Create tree {hash}", 60);
		var grid = new WormyBoardGrid (new BoardGrid (hash));
		var root = new WormyNode (meta, grid);
		root.SetRoot ();

		if (PrintToConsole) {
			Console.WriteLine ($"\n=======================================================");
			Console.WriteLine ($"WormyTreeCreator");
			root.Grid.PrintSelf ();
			Console.WriteLine ();
		}

		if (Logfile != null) {
			Directory.CreateDirectory (Path.GetDirectoryName (Logfile));
			File.WriteAllText (Logfile, "");
		}

		Solve (root, maxIterations);

		return root;
	}

	public ulong MctsSteps { get; private set; } = 0;

	private void Solve (
		WormyNode root,
		ulong maxIteration)
	{
		if (maxIteration < 1) {
			throw new ArgumentOutOfRangeException (nameof (maxIteration));
		}

		/*
		 * If not using MCTS, force abort after pruning the tree.
		 * Else, start the regular process, but start the step counter at 1.
		 */
		if (!UseMCTS) {
			var added = root.RandomPlayout (false);
			root.RecursivePrune ();
			return;
		}

		/*
		 * First of all, ALWAYS do a heuristic playout.
		 * This is likely reasonably good, and will set a proper baseline for the upper limit of costs.
		 */
		//using (var tt = new TimedOperation ($"NonrandomPlayout", 10)) {
		{
			var added = root.RandomPlayout (true);
			++MctsSteps;
		}
		//}

		while (++MctsSteps <= maxIteration) {
			if (PrintToConsole) {
				var shouldPrintNow = false;
				shouldPrintNow |= MctsSteps <= 5;
				shouldPrintNow |= MctsSteps < 100 && MctsSteps % 10 == 0;
				shouldPrintNow |= MctsSteps < 1000 && MctsSteps % 100 == 0;
				shouldPrintNow |= MctsSteps < 5000 && MctsSteps % 500 == 0;
				shouldPrintNow |= MctsSteps % 1000 == 0;
				if (shouldPrintNow) {
					//Console.WriteLine ($"\n------------------------------");
					var fpl = root.FinalizedPathLenght ();
					if (fpl >= int.MaxValue) {
						fpl = 999;
					}
					Console.WriteLine ($"Step {MctsSteps}. Fixed {fpl,2}. {root.MinCostToFinish} - {root.MaxCostToFinish}");
				}
			}

			// Simplified Monte Carlo with integrated pruning
			var res = AbstractTreeNode.MonteCarloStep (root);
			Debug.Assert (root.SolvabilityIsKnown);
			//Console.WriteLine ($"Created {res.nNodesCreated} nodes");

			if (res.someFrontierNode == null) {
				Debug.Assert (res.nNodesCreated == 0);
				// Return, do NOT perform a recursive prune
				return;
			}

			if (1111 == 111) {
				if (PrintToConsole) {
					Console.WriteLine ($"\n");
					Console.WriteLine (root.TreeAsString (
						onlyUnprunedNodes: true
						));
					Console.WriteLine ($"\n--------------------------------------------\n");
				}
			}
			if (1111 == 111 && Logfile != null) {
				var text = $"Step {MctsSteps}\n\n{root.TreeAsString ()}\n------------------------------\n\n";
				File.AppendAllText (Logfile, text);
			}

			if (1111 == 111) {
				if (PrintToConsole) {
					if (res.someFrontierNode is WormyNode wn) {
						wn.Grid.PrintSelf ();
					}
					else if (res.someFrontierNode is WormyPivot wp) {
						if (wp.Parent != null) {
							wp.Parent.Grid.PrintSelf (wp);
						}
						else {
							wp.ChildActions.FirstOrDefault ()?.Child.Grid.PrintSelf (wp);
						}
					}
					else {
						throw new Exception ();
					}
				}
			}

			GCMemoryInfo info = GC.GetGCMemoryInfo ();
			var load = info.MemoryLoadBytes;
			var avail = info.TotalAvailableMemoryBytes;
			if (avail - load < 1024L * 1024 * 1024 * 10) {
				// Console.WriteLine ($"Root has {root.Playouts} playouts; PRUNE due to memory pressure");
				PrunedAtPlayouts.Add (root.Playouts);
				root.GentlePrune ();
				MemoryPressure.Collect (true);
			}
		}

		Console.WriteLine ($"MaxIterations ({maxIteration}) was reached but the tree is not yet completely pruned. Force pruning now...");
		root.RecursivePrune ();
	}
}
