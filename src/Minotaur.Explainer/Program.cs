using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Accord.Math;
using Minotaur.Boards.Common;
using Minotaur.Boards.Dancing;
using Minotaur.Boards.Strings;
using Minotaur.Boards.TreeCaches;
using Minotaur.Explanation;
using Minotaur.GenericTreeSearch;
using Minotaur.Utils;
using static Minotaur.Utils.Logging;

namespace Minotaur;

class Program
{
	private static readonly string _Root = $"Pentomino_Explainer";
	private static readonly string _RootGen15 = $"{_Root}/Generated-15";
	private const int CurrentExplainerVersion = 67;
	private static readonly string _RootExplain = $@"{_RootGen15}\explanations\v{CurrentExplainerVersion}";

	static void Main (string [] args)
	{
		//BalancedPermutation.Selftest ();

		Directory.CreateDirectory (_RootExplain);
		Console.WriteLine ($"Loading board files from {_RootExplain}");
		var src = Directory.EnumerateFiles (_RootExplain, "*.txt", SearchOption.TopDirectoryOnly);

		var work = new List<(string name, string board)> ();

		foreach (var f in src.ToList ()) {
			var setName = Path.GetFileNameWithoutExtension (f);
			var s = File.ReadAllText (f);
			if (s.Count (c => c == '\t') == 0) {
				work.Add (new (setName, s));
				continue;
			}

			var lines = s.Split ('\n', StringSplitOptions.RemoveEmptyEntries);
			var shuffle = false;
			var before = work.Count;
			for (var iLine = 0; iLine < lines.Length; iLine++) {
				var line = lines [iLine];
				if (line.StartsWith ("#")) {
					continue;
				}
				if (line.StartsWith ("SHUFFLE")) {
					shuffle = true;
					continue;
				}
				var split = line.Split ('\t');
				var board = split [1];
				split = split.RemoveAt (1);
				var name = string.Join ("; ", split);
				name = $"{setName} {name}";
				while (work.Any (t => t.name.ToUpperInvariant () == name.ToUpperInvariant ())) {
					name = $"{name}-{iLine}";
				}
				work.Add ((name, board));
			}

			var hashes = work.Skip (before).Select (t => t.board).ToList ();
			var N_VP = 50;
			var orders = BalancedPermutation.GenerateBalancedPermutations (hashes.Count, N_VP, new Random (1));

			//for (int iVP = 0; iVP < N_VP; iVP++) {
			Parallel.For (0, N_VP, iVP => {
				var setDir = $"{_RootExplain}/sets/{setName}/VP_{iVP + 1:00}";

				var tsvPath = $"{setDir}/data.tsv";
				if (File.Exists (tsvPath)) {
					return;
				}

				var order = Enumerable.Range (0, hashes.Count).ToArray ();
				if (shuffle) {
					order = orders [iVP];
				}
				Debug.Assert (order.Length == hashes.Count);
				Debug.Assert (order.Distinct ().Length == hashes.Count);

				var tsv = "Set\tVP\tIdx\tBoard_source_index\tBoard_hash\tStrategy\tTime/F\tNotes\n";
				for (int i = 0; i < order.Length; i++) {
					tsv += $"{setName}\t";
					tsv += $"{iVP + 1}\t";
					tsv += $"{i + 1}\t";
					tsv += $"{order [i]}\t";
					tsv += $"{hashes [order [i]]}\t";
					tsv += $"?\t";
					tsv += $"?\t";
					tsv += $"?\t";
					tsv = tsv [..^1] + '\n';
				}

				string use (int index)
				{
					var largeInfo = $"""
Board {index + 1}  |  Duration: ______
""";
					largeInfo = string.Join ("\n", largeInfo.Split ('\n').Reverse ());
					return largeInfo;
				}
				var setDesc = $"{DateTime.UtcNow:yyyy-MM-dd}; set {setName}; VP {iVP:00}";
				var localBoards = order.Select (i => hashes [i]).ToList ();
				TestSetGenerator.CreateSpecifiedSet (
					setName, setDir, setDesc, localBoards,
					indexStartsAt1: true,
					largeInfo: use);

				File.WriteAllText (tsvPath, tsv);
			});
			//}
		}

		string filter;
		Console.WriteLine ("Available work:");
		Console.WriteLine (string.Join (",   ", work.Select (t => t.name)));
		Console.Write ("Board filter (press enter for no filter):  ");
		filter = Console.ReadLine ();
		Console.WriteLine ();
		work = work
			.Where (t => t.name.Contains (filter))
			.ToList ();

		Console.WriteLine ("Begin explain");
		foreach (var t in work) {
			AnalyzeSingle (t.name, t.board);
		}

		Console.WriteLine ("Program completed.");
	}

	private static void AnalyzeSingle (string name, string sboard)
	{
		var sw = Stopwatch.StartNew ();
		var dir = $"{_RootExplain}/{name}";
		Directory.CreateDirectory (dir);
		var file = $"{dir}/analyzed.txt";
		File.WriteAllText (file, sboard);

		Console.WriteLine ($"Working on {dir}");
		var ss = new Solver (sboard);
		var boardId = ss.Grid.GetCellsAsString ();

		if (1111 == 1111) {
			using var img = BoardGridDrawer.AsImage (ss.Grid, $"Empty board");
			img.Save ($"{dir}/empty_board.png");
		}

		if (1111 == 111) {
			AnalizeDance (boardId, dir);
		}
		if (1111 == 1111) {
			AnalizeWorm (boardId, name, dir);
		}

		sw.Stop ();
		var dt = sw.Elapsed.TotalSeconds;
		Console.WriteLine ($"Completed {dir} in {dt:0.0}s\n");
	}

	private static void AnalizeDance (string boardId, string dir)
	{
		var analyzePath = $"{dir}/analyze v{CurrentExplainerVersion} dance.txt";
		if (File.Exists (analyzePath)) {
			return;
		}

		var cache = new DancingTreeCreator ();
		var treeRoot = cache.CreateTree (boardId);

		var treeDir = $"{dir}/soltree dance v{CurrentExplainerVersion} L";
		var explainer = new Explainer2<BoardGrid> (treeRoot, treeDir);

		new TreeExplainer<BoardGrid> (explainer, true, new ());
		new TextExplainer<BoardGrid> (explainer, $"{dir}/flat dance");
	}

	private static void AnalizeWorm (string boardId, string name, string dir)
	{
		AbstractTreeNode.StorePruned = false;

		var solPath = $"{dir}/solution.txt";
		if (File.Exists (solPath)) {
			return;
		}

		var ss = Solver.Solved (boardId);
		if (ss.Solutions.Count > 1) {
			Console.WriteLine ($"Error: Board has more than 1 solution: {ss.Solutions.Count}");
			return;
		}
		var sol = ss.Solutions.Single ();
		var preview = Generator.SolutionToBoard (ss.Grid, sol, false);

		var info = $"expl v{CurrentExplainerVersion} {DateTime.Now:yyyyMMdd}; {name.Split (';') [0]}";
		TestSetGenerator.CreateBoardImage ($"{dir}/dina4.png", info, ss, "");

		var items = new (int maxVisits, uint steps) [] {
			(-1, 1u),
			(-1, 1_000u),
			(-1, 1_000_000u),
		};

		SaturatingCost bestResult = SaturatingCost.Infinity;
		string bestPath = null;
		long bestNodeCount = -1;

		var mctsWeightedSelect = true;
		AbstractTreeNode.MCTS_weighted_selection = mctsWeightedSelect;
		foreach (var prune in new [] { true /*, false */}) {
			AbstractTreeNode.StorePruned = !prune;
			Console.WriteLine ($"Pruning is {(prune ? "ON" : "OFF")}");

			foreach (var (maxVisits, steps) in items) {
				MemoryPressure.Collect (true);

				var wc = new WormyTreeCreator (true);
				wc.PrintToConsole = true;
				var meta = new WormyTreeCreationMeta ();
				//meta.Pool.EnabledBelowCost = 1000;
				meta.Pool.EnabledBelowCost = 0;
				meta.ForciblyPruneAfterVisits = maxVisits;
				meta.PruneImmediately = false;

				var sw = Stopwatch.StartNew ();
				var rootNode = wc.CreateTree (boardId, steps, meta);
				sw.Stop ();
				Debug.Assert (rootNode.IsSolvable == true);

				Debug.Assert (rootNode.MinCostToFinish == rootNode.MaxCostToFinish);
				var cost = rootNode.MaxCostToFinish;

				var mcts_stats_row = "v1"
					+ $"\t{mctsWeightedSelect}"
					+ $"\t{prune}"
					+ $"\t{maxVisits}"
					+ $"\t{steps}"
					+ $"\t{cost}"
					+ $"\t{sw.Elapsed.TotalSeconds:0.000}"
					+ $"\t{name}"
					+ $"\t{boardId}"
					+ $"\t{string.Join (",", wc.PrunedAtPlayouts)}"
					+ "\n";
				File.AppendAllText ($"{dir}/../mcts_test.tsv", mcts_stats_row);

				var e = $"p={(prune ? 1 : 0)}" +
					$", e{Math.Log10 (maxVisits):0.0} visits" +
					$", e{Math.Log10 (steps):0.0} steps" +
					$", {Math.Log10 (cost.AsUlong ()):0.0} cost";
				var treeDir = $"{dir}\\soltree string, {e}";
				var explainer = new Explainer2<WormyBoardGrid> (rootNode, treeDir);

				// For paper
				Dictionary<int, Color> cellColors = new ();
				if (dir.EndsWith ("A7-080 vs 576")) {
					cellColors [28] = Color.FromArgb (233, 212, 117);
					cellColors [1] = Color.FromArgb (99, 224, 193);
					cellColors [9] = Color.FromArgb (242, 195, 123);
					cellColors [37] = Color.FromArgb (156, 206, 225);
					cellColors [18] = Color.FromArgb (186, 132, 184);
				}

				new TreeExplainer<WormyBoardGrid> (explainer, true, cellColors);
				if (1111 == 1111) {
					new TextExplainer<WormyBoardGrid> (explainer, $"{dir}/flat string, {e}");
				}

				if (cost < bestResult) {
					bestResult = cost;
					bestPath = treeDir;
					bestNodeCount = rootNode.CountTreeNodes ();
				}

				if (maxVisits >= 0) { // for <0, the system adapts automatically
					if (sw.Elapsed.TotalMinutes > 1) {
						// Probably not possible to do more
						break;
					}
				}
			}
		}

		// marks this as complete
		File.WriteAllText (solPath, preview);

		var pngname = $"{name}";
		var dst = $"{dir}/../{pngname}.png";
		File.Copy ($"{bestPath}.png", dst, true);
	}
}
