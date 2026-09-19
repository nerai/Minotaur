using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ANSIConsole;
using Minotaur.Boards.Common;
using Minotaur.Boards.Dancing;
using Minotaur.Boards.Postgre;
using Minotaur.Boards.Strings;
using Minotaur.Boards.TreeCaches;
//using Minotaur.Cumulatives;
using Minotaur.GenericTreeSearch;
using Minotaur.Utils;
using Minotaur;
//using Minotaur.Difficulty;
//using Minotaur.Difficulty.Sampling;
using static Minotaur.Utils.Logging;

namespace Minotaur;

class Program
{
	public static readonly List<Piece> PieceSet_Active = new ();

	public static int Target_SX = 10;
	public static int Target_SY = 6;
	public static int Target_PieceCount = 12;

	private static readonly string _Root = $"Pentomino_root";
	private static readonly string _RootGen14 = $"{_Root}/Generated-14";

	const string UsageHelp = ""
		+ "PentominoSolver"
		+ "Sebastian Heuchler 2022-2026"
		+ ""
		+ "--remove PIECES"
		+ "Removes pieces identfied by letters from the 12 default pieces provided."
		+ ""
		+ "--size X Y"
		+ "Sets board size to X, Y integers. Default: 10x6."
		+ ""
		+ "--mode MODE"
		+ "Sets mode to MODE, one of:"
		+ "generate\tGenerate new boards. Default mode."
		+ "solve\tSolve the board."
		+ "conjecture\tSolve 10 piece conjecture."
		+ "auto\tAutomatically generate many boards for many parameters."
		+ ""
		+ ""
		+ "Additional options for generating new boards:"
		/*
		+ ""
		+ "--min N"
		+ "Filters generated boards to have at least N solutions. Default: 1"
		+ ""
		+ "--max N"
		+ "Filters generated boards to have at most N solutions. Default: 1"
		*/
		+ ""
		+ "--count K"
		+ "Generated boards are randomly filled until they have space for exactly K pieces."
		+ ""
		+ ""
		+ "Additional options for solving an existing board:"
		+ "(none)"
		+ "";

	static void Main (string [] args)
	{
		// Colored console output
		if (1111 == 111) {
			if (!ANSIInitializer.Init (false)) {
				ANSIInitializer.Enabled = false;
			}
		}

		Console.Title = $"Pentomino [{Environment.ProcessId}] <{Environment.CommandLine}>";
		Process.GetCurrentProcess ().PriorityClass = ProcessPriorityClass.Idle;

		//Generator.StartBackgroundImport ();

		{
			ThreadPool.GetMinThreads (out var workers, out var ports);
			Console.WriteLine ($"ThreadPool.GetMinThreads: {workers} workers, {ports} ports");
			ThreadPool.GetMaxThreads (out workers, out ports);
			Console.WriteLine ($"ThreadPool.GetMaxThreads: {workers} workers, {ports} ports");
		}

		string mode = "";
		string removePieces = null;

		for (int i = 0; i < args.Length; i++) {
			switch (args [i]) {
				case "--remove":
					removePieces = args [++i];
					break;
				case "--size":
					Target_SX = int.Parse (args [++i]);
					Target_SY = int.Parse (args [++i]);
					break;
				case "--mode":
					mode = args [++i];
					break;
				case "--count":
					Target_PieceCount = int.Parse (args [++i]);
					break;
				default:
					Console.WriteLine ($"Did not understand argument <{args [i]}>, aborting.");
					Console.WriteLine (UsageHelp);
					return;
			}
		}

		CreatePieces (removePieces);

		switch (mode) {
			case "CreatePieceInsertStatistics":
				CreatePieceInsertStatistics ();
				break;

			case "FindAmbiguousExamples":
				FindAmbiguousExamples ();
				break;

			case "prove-symmetric-10":
				ProveSymmetric10 ();
				break;

			case "solve":
				FindAllSolutions (PieceSet_Active);
				break;

			case "generate":
				var n = 1_000_000_000_000ul;
				var skip = 0ul;
				var gen = new Generator (
					Target_SX,
					Target_SY,
					Target_PieceCount,
					n,
					TimeSpan.MaxValue,
					false,
					true,
					filter: null,
					skipBoards: skip);
				break;

			case "conjecture":
				Prove10PieceConjecture ();
				break;

			case "boardcounts":
				PrintBoardCounts ();
				break;

			case "cumulative":
				OpenCumulativeDB ();
				break;

			case "auto":
				Autogenerate ();
				break;

			case "sets":
				GenerateTestSet ();
				break;

			case "write-samples":
				WriteSampleFile ();
				break;

			case "test-tree-random-influence":
				TestTreeRandomInfluence ();
				break;

			default:
				throw new InvalidOperationException ();
		}

		Logging.LogN ("Program completed");
	}

	private static void FindAmbiguousExamples ()
	{
		var sx = 6;
		var sy = 4;
		var nps = 2;
		var bs = new BoardStorePG (false).WithBoards (bs => bs
			.Where (b => b.SX == sx)
			.Where (b => b.SY == sy)
			.Where (b => b.NPieces == nps)
			.Where (b => b.SolutionCount == 3)
			.ToArray ());

		Console.WriteLine ($"There are {bs.Length} boards of size {sx}x{sy} with {nps} pieces and 3 solutions.");
		Console.WriteLine ($"Finding ambiguous solutions...");

		var seen = new HashSet<string> ();
		foreach (var b in bs) {
			var ss = new Solver (b.InvariantGridName);
			ss.Solve (false);
			var sets = ss.Solutions
				.Select (sol => {
					var pls = sol
						.Select (placement => placement.P.Name)
						.OrderBy (name => name);
					return string.Join ("", pls);
				})
				.ToArray ();
			if (sets.Distinct ().Count () < 3) {
				var involvedPlacements = ss.Solutions
					.SelectMany (sol => sol)
					.Select (pl => $"{pl.P},{pl.Rot},{pl.X},{pl.Y}")
					.Distinct ()
					.Count ();
				Console.WriteLine (involvedPlacements);
				if (involvedPlacements < nps * 3) {
					continue;
				}

				var ssets = string.Join (", ", sets.OrderBy (set => set));
				if (seen.Add (ssets)) {
					Console.WriteLine ("-------------------");
					Console.WriteLine (ssets);
					Console.WriteLine (ss.Grid.TextualRepresentation (BoardGrid.TextualRepresentationStyle.Condensed));
					foreach (var sol in ss.Solutions) {
						var preview = Generator.SolutionToBoard (ss.Grid, sol, false);
						Console.WriteLine (preview);
						Console.WriteLine ();
					}
				}
			}
		}
	}

	private static void ProveSymmetric10 ()
	{
		Target_PieceCount = 2;

		var t = DateTime.UtcNow;
		long nSym = 0;
		long nAsym = 0;

		void Stats ()
		{
			var dt = DateTime.UtcNow - t;
			Console.WriteLine ($"Encountered {Interlocked.Read (ref nSym)} symmetric and {Interlocked.Read (ref nAsym)} asymmetric boards in {dt.TotalSeconds:0.0}s");
		}

		bool Filter (BoardGrid b)
		{
			var sym = b.IsPointSymmetric ();
			if (sym) {
				long k = Interlocked.Increment (ref nSym);
				if (k % 1000 == 0) {
					Stats ();
				}
			}
			else {
				Interlocked.Increment (ref nAsym);
			}
			return sym;
		}

		/*
		 * The size is limited: sx + sy <= 10
		 */
		var sizes = new [] {
			(9, 2),
			(8, 3),
			(7, 4),
			(6, 5),
		};

		foreach (var size in sizes) {
			Console.WriteLine ($"\n\n--------------\nSearching size {size}");
			Target_SX = size.Item1;
			Target_SY = size.Item2;
			var gen = new Generator (
				Target_SX,
				Target_SY,
				Target_PieceCount,
				long.MaxValue,
				TimeSpan.MaxValue,
				false,
				true,
				filter: Filter);
			gen.PrintStats ();
		}

		Stats ();
	}

	private static void CreateExperimentDataForHashes ()
	{
		/* requires DB access
		var setWithPrefix = File.ReadAllLines ("ExperimentData//1.tsv")
			.Where (line => !line.StartsWith ("#"))
			.Select (line => line.Split (';') [0])
			.ToList ();
		var setWithoutPrefix = setWithPrefix
			.Select (line => line [5..])
			.ToList ();
		var gen = GenerateDataForStrings (setWithoutPrefix);

		var setName = $"{DateTime.UtcNow:yyyy.MMdd.HHmm}.{0:0}";
		var setDir = $"{_RootGen14}/sets/{setName}";
		var setDesc = $"Recreated Set={setName}";
		var fromDir = $@"";
		TestSetGenerator.CreateSpecifiedSet (setName, setDir, setDesc, setWithPrefix);
		*/
	}

	private static void CreatePieces (string remove)
	{
		PieceSet_Active.AddRange (Piece.AllPieces);
		if (remove != null) {
			foreach (var c in remove) {
				if (PieceSet_Active.RemoveAll (p => p.Name == c.ToString ()) == 0) {
					Console.WriteLine ($"Could not remove piece {c}.");
				}
			}
		}
	}

	private static void Prove10PieceConjecture ()
	{
		Console.WriteLine ("Proving 5x10 - 2P = 65 conjecture");

		Target_SX = 10;
		Target_SY = 5;
		Target_PieceCount = 10;
		var dict = new Dictionary<string, int> ();

		Parallel.For (0, 12 * 12, i => {
			int a = i % 12;
			int b = i / 12;
			if (b <= a) {
				return;
			}
			var s = $"{Piece.AllPieces [a].Name},{Piece.AllPieces [b].Name}";

			var pieces = Piece.AllPieces.ToList ();
			pieces.RemoveAt (b);
			pieces.RemoveAt (a);

			var solved = FindAllSolutions (pieces);
			lock (dict) {
				dict.Add (s, solved.Solutions.Count);
				Console.WriteLine ($"{s} has {solved.Solutions.Count,4} solutions, solved in {solved.Steps,6} steps.");
			}
		});

		Console.WriteLine ($"{dict.Values.Count (i => i == 0)} out of {dict.Count} configurations have no solution.");
	}

	private static Solver FindAllSolutions (List<Piece> movables)
	{
		var statics = Array.Empty<Piece> ();

		if (Target_SX * Target_SY % 5 != 0) {
			throw new ArgumentException ("Target_SX * Target_SY % 5 != 0");
		}
		var grid = new BoardGrid (Target_SX, Target_SY);

		var start = DateTime.UtcNow;
		var ss = new Solver (movables, statics, grid);
		ss.Solve (false);

		if (true) {
			var dt = DateTime.UtcNow - start;
			Console.WriteLine ($"Found {ss.Solutions.Count} solutions in {dt.TotalSeconds:0.0}s.");
		}

		return ss;
	}

	private static void PrintBoardCounts ()
	{
		/* requires DB access
		var file = $"board statistics {DateTime.UtcNow:yyyy.MMdd.HHmmss}.txt";
		var bs = new BoardStorePG (false);
		void print (string s)
		{
			Console.Write (s);
			File.AppendAllText (file, s);
		}

		print ($"% Statistics generated {DateTime.UtcNow}\n");
		print ($"\n");
		print ($"\\begin{{tabularx}}{{\\textwidth}}{{");
		print ($"r|r|r"); // size_x, size_y, N_pieces
		print ($"|r"); // total
		print ($"|rr"); // canonical, perc
		print ($"|rr"); // solvable, perc
		print ($"|rr"); // unique sol, perc
		print ($"}}\n");

		print ($"\\thead{{X}} & \\thead{{Y}} & \\thead{{N}}");
		print ($" & \\thead{{Total}} & \\thead{{Ess. diff.}} & (perc)");
		print ($" & \\thead{{Solvable}} & (perc)");
		print ($" & \\thead{{Unique\\\\solution}} & \\thead{{(perc)}}");
		print ($"\\Xhline{{2pt}}\n");

		for (Target_SX = 1; Target_SX <= 14; Target_SX++) {
			for (Target_SY = 1; Target_SY <= Target_SX; Target_SY++) {
				var max_pieces = Target_SX * Target_SY / 5;
				max_pieces = Math.Min (max_pieces, 12);

				for (Target_PieceCount = 1; Target_PieceCount <= max_pieces; Target_PieceCount++) {
					var permutationCount = BoardGrid.NumberOfBoards (Target_SX, Target_SY, Target_PieceCount);

					var nodupe_db = bs.WithBoards (bs => bs
						.Where (b => true
							&& b.SX == Target_SX
							&& b.SY == Target_SY
							&& b.NPieces == Target_PieceCount)
						.LongCount ());
					var nodupe_real = Counting.CanonicalBoardCount (Target_SY, Target_SX, 5 * Target_PieceCount);

					var solvable = bs.WithBoards (bs => bs
						.Where (b => true
							&& b.SX == Target_SX
							&& b.SY == Target_SY
							&& b.NPieces == Target_PieceCount)
						.Where (b => b.SolutionCount > 0)
						.LongCount ());

					var uniquesol = bs.WithBoards (bs => bs
						.Where (b => true
							&& b.SX == Target_SX
							&& b.SY == Target_SY
							&& b.NPieces == Target_PieceCount)
						.Where (b => b.SolutionCount == 1)
						.LongCount ());

					string percent (BigInteger num, BigInteger den)
					{
						double perc;
						if (den.IsZero) {
							perc = 0;
						}
						else {
							var b = new BigInteger (100_000);
							b *= num;
							b /= den;
							perc = ((double) b) / 1000.0;
						}
						return $"{perc:0.0}\\%";
					}

					string approx (double n)
					{
						var e = 0;
						while (n >= 10) {
							++e;
							n /= 10;
						}
						return $"$\\approx {n:0.0} \\cdot 10^{e:0}$";
					}

					string formatBig (BigInteger n)
					{
						if (n < 1_000_000_000_000) {
							return $"{n:#,##0}";
						}
						else {
							return approx ((double) n);
						}
					}

					print ($"\\\\\\hline ");
					print ($"{Target_SX,2} & {Target_SY,2} & {Target_PieceCount,2} & ");

					print ($"{formatBig (permutationCount),16}");

					var precise = nodupe_db == nodupe_real;
					if (nodupe_db > nodupe_real) {
						Debugger.Break ();
						//throw new InvalidDataException ("DB is messed up.");
					}
					if (permutationCount < 90_000_000 && !precise) {
						Debugger.Break ();
						//throw new InvalidDataException ("DB is messed up.");
					}

					var sNodupeN = formatBig (nodupe_real);
					var sNodupeP = $"{percent (nodupe_real, permutationCount)}";
					sNodupeN = $"(actual in DB: {nodupe_db,12:#,##0}) {sNodupeN,14}"; //xxx
					print ($" & {sNodupeN,25} & {sNodupeP,7}");

					string sSolvableN;
					string sSolvableP;
					string sUniquesolN;
					string sUniquesolP;

					if (precise) {
						// these are complete
						sSolvableN = $"{solvable:#,##0}";
						sSolvableP = $"{percent (solvable, nodupe_db)}";
						sUniquesolN = $"{uniquesol:#,##0}";
						sUniquesolP = $"{percent (uniquesol, solvable)}";
					}
					else {
						sSolvableN = approx (solvable);
						sSolvableP = $"$\\approx$ {percent (solvable, nodupe_db)}";
						sUniquesolN = approx (uniquesol);
						sUniquesolP = $"$\\approx$ {percent (uniquesol, solvable)}";
					}

					print ($" & {sSolvableN,25} & {sSolvableP,16}");
					print ($" & {sUniquesolN,25} & {sUniquesolP,16}");
					print ($"\n");
				}
			}
		}
		print (@"\end{tabularx}\\n");
		*/
	}

	private static void OpenCumulativeDB ()
	{
		/* requires DB access
		var db = new CumulativePentoDb ();
		*/
	}

	private static void Autogenerate ()
	{
		Console.Write ("Enter the skip value of this program instance [format: XXYYPP; 0 = no skip]:  ");
		var skip = int.Parse (Console.ReadLine ());
		Console.WriteLine ();

		for (Target_SX = 1; Target_SX <= 14; Target_SX++) {
			for (Target_SY = 1; Target_SY <= Target_SX; Target_SY++) {
				var max_pieces = Target_SX * Target_SY / 5;
				max_pieces = Math.Min (max_pieces, 12);

				for (Target_PieceCount = 1; Target_PieceCount <= max_pieces; Target_PieceCount++) {
					if (Target_SX * 10000 + Target_SY * 100 + Target_PieceCount < skip) {
						continue;
					}

					var name = $"{Target_SX}x{Target_SY}/{Target_PieceCount}";
					var permutationCount = BoardGrid.NumberOfBoards (Target_SX, Target_SY, Target_PieceCount);

					ulong target_count;
					bool generate_all;

					if (permutationCount > 10_000_000) {
						target_count = 100_000;
						generate_all = false;
					}
					else {
						target_count = ulong.MaxValue;
						generate_all = true;
					}

					var gen = new Generator (
						Target_SX,
						Target_SY,
						Target_PieceCount,
						target_count,
						TimeSpan.MaxValue,
						true,
						generate_all,
						skipBoards: 0,
						checkDupeBeforeSolving: false);
				}
			}
		}
	}

	public static Generator GenerateDataForStrings (IEnumerable<string> hashes)
	{
		var boards = hashes.Select (hash => new BoardGrid (hash)).ToList ();
		var sx = boards.Select (b => b.SX).Distinct ().Single ();
		var sy = boards.Select (b => b.SY).Distinct ().Single ();
		var nPcs = boards.Select (b => b.OpenTileCount).Distinct ().Single () / 5;

		var gen = new Generator (
			sx,
			sy,
			nPcs,
			0,
			TimeSpan.Zero,
			true,
			true);
		gen.TrySolveAndAddBoards (boards, true);
		return gen;
	}

	private static void GenerateTestSet ()
	{
		/* requires DB access
		var tsg = new TestSetGenerator (7, 6, 5, _RootGen14);
		*/
	}

	private static IEnumerable<string> ReadSamples (int sx, int sy, int npieces)
	{
		var q = new BlockingCollection<string> (100_000);

		new Thread (() => {
			void ReadIntoQ (IQueryable<DbBoard> qq)
			{
				long nIn = 0;
				var src = qq
					.Where (b => b.SX == sx && b.SY == sy && b.NPieces == npieces && b.SolutionCount == 1)
					.Select (b => b.Preview);
				foreach (var raw in src) {
					if (++nIn % 1000000 == 0) {
						Console.WriteLine ($"Read {nIn / 1000000}m rows, queue length {q.Count}/{q.BoundedCapacity}");
					}
					q.Add (raw);
				}
				q.CompleteAdding ();
			}
			new BoardStorePG (false).WithBoards (ReadIntoQ);
		}) {
			Name = "DB read board previews"
		}
		.Start ();

		var processed = q
			.GetConsumingEnumerable ()
			.AsParallel ()
			.Select (raw => raw.Replace ("\n", ""));

		int nOut = 0;
		foreach (var preview in processed) {
			if (++nOut % 1000000 == 0) {
				Console.WriteLine ($"{nOut / 1000000}m rows");
			}
			yield return preview;
		}
	}

	private static void WriteSampleFile ()
	{
		int written = 0;
		using var f = new StreamWriter ("samples.txt");
		foreach (var sample in ReadSamples (7, 6, 5)) {
			var s = sample;
			f.Write (s);
			f.Write ('\n');

			written++;
			if (written % 1000000 == 0) {
				Console.WriteLine ($"Wrote {written:#,##0} samples.");
			}
		}
		Console.WriteLine ($"Finished writing {written:#,##0} samples");
	}

	private static void TestTreeRandomInfluence ()
	{
		/* requires DB access
		new DifficultBoardEvaluation_2025 ();
		*/
	}

	private static void CreatePieceInsertStatistics ()
	{
		/* requires DB access
		var baseDir = "piece-insert-statistics";
		var dir = $"{baseDir}/{DateTime.UtcNow:yyyy.MMdd.HHmm.ssff}";
		Directory.CreateDirectory (dir);
		Console.WriteLine ($"CreatePieceInsertStatistics, store to {dir}");

		for (int np = 1; np <= 12; np++) {
			using var sw = new StreamWriter ($"{dir}/rows per piece, P={np}.tsv");
			sw.Write ($"X\tY\tP\t");
			sw.Write ($"Index\t");
			//sw.Write ($"Grid\t");
			sw.Write ($"Piece\t");
			sw.Write ($"Rows\n");

			for (int sx = 1; sx <= 14; sx++) {
				for (int sy = 1; sy <= sx; sy++) {
					List<string> samples;
					using (var to = new TimedOperation ($"Query DB for most difficult {sx}x{sy}/{np} boards", 60)) {
						samples = new BoardStorePG (true).WithBoards (qq => qq
							.Where (b => b.SX == sx && b.SY == sy && b.NPieces == np && b.SolutionCount == 1)
							.OrderByDescending (b => b.SolutionSteps)
							.ThenBy (b => b.InvariantGridName)
							.Select (b => b.InvariantGridName)
							.Take (100_000)
							.ToList ());
						lock (Console.Out) {
							Console.WriteLine ($"For {sx}x{sy}/{np} got {samples.Count} sample grids.");
						}
					}
					using (var to = new TimedOperation ($"Piece statistics for {samples.Count} difficult {sx}x{sy}/{np} boards", 10)) {
						_ = Parallel.ForEach (samples, (grid, _, i) => {
							//for (int i = 0; i < samples.Count; i++) { var grid = samples [i];
							var name = $"{sx:00}{sy:00}{np:00}/{grid}";
							var ss = new Solver (grid);
							var possibleInsertLocationsHistogram = new ulong [12];
							ss.Solve (false,
								possibleInsertLocationsHistogram: possibleInsertLocationsHistogram);

							lock (sw) {
								for (int p = 0; p < 12; p++) {
									sw.Write ($"{sx}\t{sy}\t{np}\t");
									sw.Write ($"{i}\t");
									//sw.Write ($"{grid}\t");
									sw.Write ($"{Piece.AllPieces [p].Name}\t");
									sw.Write ($"{possibleInsertLocationsHistogram [p]}\n");
								}
							}
						});
					}
				}
			}
		}
		*/
	}

	private static void GenerateStepCountHistogramData ()
	{
		for (int np = 4; np <= 12; np++) {
			for (int sx = 1; sx <= 14; sx++) {
				for (int sy = 1; sy <= sx; sy++) {
					int cells = sx * sy;
					int open = np * 5;
					int closed = cells - open;
					if (open > cells) {
						continue;
					}

					var name = $"{sx:00},{sy:00},{np:00}";
					var sb = new StringBuilder ();

					sb.AppendLine ($"@echo Create histogram for {name}");

					sb.Append ($">{name}.tmp ");
					sb.Append (".\\PentominoCPP.exe --table \"");
					for (int i = 0; i < cells; i++) {
						if ((i > 0) && (i % sx == 0)) {
							sb.Append (',');
						}
						if (i < open) {
							sb.Append ('0');
						}
						else {
							sb.Append ('1');
						}
					}
					sb.Append ($"\"");
					sb.AppendLine ();

					sb.AppendLine ($"move {name}.tmp {name}.txt");

					File.WriteAllText ($"Generate histogram for {name}.bat", sb.ToString ());
				}
			}
		}
	}
}
