using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Minotaur.Boards.Common;
using Minotaur.Boards.Postgre;
using Minotaur.Utils;

using static Minotaur.Utils.Logging;

namespace Minotaur.Boards.Dancing;

public class Generator
{
	public readonly IReadOnlyList<Piece> PieceSet;

	public readonly int SX;
	public readonly int SY;
	public readonly int NPcs;
	public string SXYP => $"{SX,2}x{SY,2}/{NPcs,2} ";

	public readonly bool CheckDupeBeforeSolving;

	public readonly BoardStorePG BS;

	private readonly BlockingCollection<DbBoard> _Insert = new (2000);

	private readonly DateTime _Start;
	private BigInteger SkippedBoards = 0;
	private BigInteger GoalBoardCount = 0;
	private ulong EnumeratedInSession = 0;
	private long nNotCanonical = 0;
	private long nSolvable = 0;
	private long nUnsolvable = 0;
	private long FoundDupes = 0;
	private long StoredBoards = 0;

	private long _LastStatsTicks = Environment.TickCount64;
	private ulong _LastStatsEnumerated = 0;

	public const int MaxExportedSolutions = 1;

	public Action<List<BoardGrid>> CanonicalBoardsConsidered;

	private static readonly ResourceAllocator _ParallelGenerators = new (
		"Dancing::Generator",
		2,
		MultiwaitSemaphore.PrintOptionFlags.pass |
		MultiwaitSemaphore.PrintOptionFlags.wait |
		MultiwaitSemaphore.PrintOptionFlags.continu |
		0);

	public Generator (
		int sx,
		int sy,
		int pieceCount,
		ulong boardLimit,
		TimeSpan timeLimit,
		bool storeUnsolvable,
		bool generateAll,
		Func<BoardGrid, bool> filter = null,
		BigInteger? skipBoards = null,
		bool checkDupeBeforeSolving = true,
		Action<List<BoardGrid>> canonicalBoardsConsidered = null)
	{
		PieceSet = Piece.AllPieces;
		SX = sx;
		SY = sy;
		NPcs = pieceCount;

		CheckDupeBeforeSolving = checkDupeBeforeSolving;
		CanonicalBoardsConsidered = canonicalBoardsConsidered;

		var permutationCount = BoardGrid.NumberOfBoards (SX, SY, NPcs);
		SkippedBoards = skipBoards ?? BigInteger.Zero;
		skipBoards = null;
		GoalBoardCount = SkippedBoards + boardLimit;
		if (GoalBoardCount > permutationCount) {
			GoalBoardCount = permutationCount;
			boardLimit = (ulong) (GoalBoardCount - SkippedBoards);
		}
		if (boardLimit <= 0) {
			return;
		}

		using var rented = _ParallelGenerators.Rent (1, $"solve {boardLimit:#,##0} boards {SX}x{SY}/{NPcs}");

		using (var _ = StartLogBlock) {
			LogN ();
			Log (ConsoleColor.Magenta, $"{SXYP}");
			Log ($"Start at {SkippedBoards:#,##0}, consider up to {boardLimit:#,##0} boards within {timeLimit.TotalSeconds:0}s");
			LogN ();
			Log ($"There are {permutationCount:#,##0} boards with these parameters. ");
			Log ($"Enumeration will be {(generateAll ? "complete" : "randomized")}. ");
			Log ($"Unsolvable boards will {(storeUnsolvable ? "" : "NOT ")}be persisted.");
			LogN ();
		}

		BS = new BoardStorePG (true);
		var sw = Stopwatch.StartNew ();
		_Start = DateTime.UtcNow;
		var cancelBoardGeneration = new CancellationTokenSource ();

		var boardWorkQueue = new BlockingCollection<SmallBitArray []> (10);
		void FindBoardsThread ()
		{
			var buf = new List<SmallBitArray> ();
			bool SubmitBuffer ()
			{
				if (buf.Count == 0) {
					return true;
				}

				try {
					boardWorkQueue.Add (buf.ToArray (), cancelBoardGeneration.Token);
					buf.Clear ();
					return true;
				}
				catch (OperationCanceledException) {
					return false;
				}
			}

			IEnumerable<SmallBitArray> src;
			if (generateAll) {
				src = BoardGrid.GenerateAll (SX, SY, NPcs, SkippedBoards);
			}
			else {
				src = BoardGrid.GenerateRandom (SX, SY, NPcs);
			}
			var e = src.GetEnumerator ();

			ulong n = 0;
			while (n < boardLimit) {
				if (!e.MoveNext ()) {
					break;
				}
				buf.Add (e.Current);
				if (buf.Count >= 1000) {
					if (!SubmitBuffer ()) {
						break;
					}
				}
				n++;
				PrintStats ();
			}
			SubmitBuffer ();
			boardWorkQueue.CompleteAdding ();
		}

		var tFindBoards = new Thread (FindBoardsThread) {
			Name = $"Generator {SX}x{SY}/{NPcs}: FindBoards",
			IsBackground = false,
		};
		tFindBoards.Start ();
		if (timeLimit < TimeSpan.FromDays (400)) {
			cancelBoardGeneration.CancelAfter (timeLimit);
		}

		var tWriteDB = new Thread (DbWriteThread) {
			Name = $"Generator {SX}x{SY}/{NPcs}: WriteDB",
			IsBackground = false,
		};
		tWriteDB.Start ();

		int nThreads;
		if (false && Debugger.IsAttached) {
			LogN ($"{SXYP} Debugger is attached, solving boards serially.");
			nThreads = 1;
		}
		else {
			LogN ($"{SXYP} Solving boards in parallel.");
			nThreads = Environment.ProcessorCount;
		}
		CustomParallel.ForEach (
			$"Generator {SX}x{SY}/{NPcs}: Solver",
			nThreads,
			$"Generator Solver",
			(uint) Environment.ProcessorCount,
			0,
			ThreadPriority.Lowest,
			60,
			boardWorkQueue.GetConsumingEnumerable (),
			perms => {
				var grids = perms
					.Select (perm => new BoardGrid (SX, SY, perm))
					.Where (grid => filter == null || filter (grid))
					.ToList ();
				TrySolveAndAddBoards (grids, storeUnsolvable);
			});

		tFindBoards.Join ();
		LogN ($"{SXYP} Joining DB writer thread...");
		_Insert.CompleteAdding ();
		tWriteDB.Join ();

		sw.Stop ();
		using (var _ = StartLogBlock) {
			LogN ();
			Log (Background, ConsoleColor.Magenta);
			var s = $"{SXYP} Generator "
				+ $" from {SkippedBoards:#,##0} plus {EnumeratedInSession:#,##0}"
				+ $" finished in {sw.Elapsed.TotalSeconds:0.0}s!";
			Log (s, ResetColor);
		}
	}

	public void PrintStats ()
	{
		var ticks = Environment.TickCount64;
		var msSinceLast = ticks - _LastStatsTicks;
		if (msSinceLast < 60 * 1000) {
			return;
		}

		var enumerated = Interlocked.Read (ref EnumeratedInSession);
		var ticksPerBoard = 1.0 * msSinceLast / (enumerated - _LastStatsEnumerated);
		var remainingTicks = (ulong) (GoalBoardCount - SkippedBoards - enumerated) * ticksPerBoard;
		var solved = Interlocked.Read (ref nSolvable) + Interlocked.Read (ref nUnsolvable);
		var remainingHours = remainingTicks / 1000 / 60 / 60;
		var tComplete = remainingHours > 24 * 365
			? "> 1 year"
			: $"{DateTime.Now.AddHours (remainingHours):ddd MM-dd HH:mm}";
		var dtStart = DateTime.UtcNow - _Start;
		var percComplete = 100.0 * enumerated / Math.Max (1, (ulong) (GoalBoardCount - SkippedBoards));

		using (var _ = StartLogBlock) {
			LogN (
				$"{SXYP} ",
				ConsoleColor.Magenta,
				$"Enumerated",
				ResetColor,
				$" {SkippedBoards:#,##0} plus {enumerated:#,##0} boards ({percComplete:0.000}%) in {dtStart.TotalSeconds:0}s",
				$" ({ticksPerBoard:0.0}ms each)",
				$", {solved:#,##0} solved",
				$", {StoredBoards:#,##0} stored",
				$", {FoundDupes:#,##0} dupe",
				$"; {remainingHours:#,##0.0}h remain ({tComplete})");
		}

		_LastStatsTicks = ticks;
		_LastStatsEnumerated = enumerated;
	}

	private void CheckCandidateBoards (List<BoardGrid> grids)
	{
		Interlocked.Add (ref EnumeratedInSession, (ulong) grids.Count);

		grids.RemoveAll (grid => {
			if (grid.InvariantName == grid.NotInvariantName) {
				return false;
			}

			// This is not the canonical grid representation
			// Ignore those - then there will be no duplicate inserts!
			// This also avoids the bug where the JSON solution belongs to a different grid variation
			if (Interlocked.Increment (ref nNotCanonical) % 8_000 == 0) {
				lock (Console.Out) {
					//Console.Write ("@");
				}
			}
			return true;
		});
		if (grids.Count == 0) {
			return;
		}

		CanonicalBoardsConsidered?.Invoke (grids);

		if (CheckDupeBeforeSolving) {
			var gridnames = grids
				.Select (grid => grid.InvariantName)
				.ToArray ();

			string [] dupes;
			while (true) {
				try {
					dupes = BS.WithBoards (bs => bs
						 .Select (b => b.InvariantGridName)
						 .Where (name => gridnames.Contains (name))
						 .ToArray ());
					break;
				}
				catch (Exception ex) {
					LogN (ex);
				}
			}

			int removed = grids.RemoveAll (grid => dupes.Contains (grid.InvariantName));

			var newDupeCount = Interlocked.Add (ref FoundDupes, dupes.Length);
			if (newDupeCount / 1000 != (newDupeCount - dupes.Length) / 1000) {
				lock (Console.Out) {
					//Console.Write ("=");
				}
			}
		}
	}

	public void TrySolveAndAddBoards (List<BoardGrid> grids, bool storeUnsolvable)
	{
		CheckCandidateBoards (grids);
		foreach (var grid in grids) {
			SolveAndAddBoard (grid, storeUnsolvable);
		}
	}

	private void SolveAndAddBoard (BoardGrid grid, bool storeUnsolvable)
	{
		var ss = SolveBoard (grid);
		if (ss.Solutions.Count == 0) {
			if (Interlocked.Increment (ref nUnsolvable) % 1_000 == 0) {
				//LogF ("-");
			}
		}
		else {
			if (Interlocked.Increment (ref nSolvable) % 1_000 == 0) {
				//LogF ("+");
			}
		}

		if ((ss.Solutions.Count == 0) && !storeUnsolvable) {
			return;
		}

		string preview = null;
		if (ss.Solutions.Count > 0) {
			preview = SolutionToBoard (grid, ss.Solutions.FirstOrDefault (), true);
		}
		var b = new DbBoard (
			grid.InvariantName,
			(byte) grid.SX,
			(byte) grid.SY,
			(byte) NPcs,
			ss.Solutions.Count,
			preview,
			ss.Steps);
		_Insert.Add (b);
	}

	private Solver SolveBoard (BoardGrid grid)
	{
		var statics = Array.Empty<Piece> ();
		var ss = new Solver (PieceSet, statics, grid);
		ss.Solve (false);
		return ss;
	}

	private void DbWriteThread ()
	{
		foreach (var b in _Insert.GetConsumingEnumerable ()) {
			var buffer = new List<DbBoard> () { b };
			while (buffer.Count < 1000) {
				var t = 5000 / buffer.Count;
				if (_Insert.TryTake (out var b2, t)) {
					buffer.Add (b2);
				}
				else {
					break;
				}
			}

			//Console.WriteLine ($"Inserting {buffer.Count} boards ({_Insert.Count} in queue)");
			var added = BS.AddBoards (buffer);
			var total = Interlocked.Add (ref StoredBoards, added);
			if ((total - added) % 1000 + added >= 1000) {
				//LogF (".");
			}
		}
		LogN ($"{SXYP} Inserter thread completed.");
	}

	public static string SolutionToBoard (BoardGrid grid, List<Placement> solution, bool compact)
	{
		char [] cs = new char [grid.SY * grid.SX];

		if (solution != null) {
			foreach (var pl in solution) {
				var p = pl.P;
				for (int y = 0; y < p._SY; y++) {
					for (int x = 0; x < p._SX; x++) {
						if (p._Fill [x + y * p._SX]) {
							cs [(pl.X + x) + (pl.Y + y) * grid.SX] = p.Name [0];
						}
					}
				}
			}
		}

		var sb = new StringBuilder ();
		for (int y = 0; y < grid.SY; y++) {
			if (y > 0) {
				sb.Append ('\n');
			}
			for (int x = 0; x < grid.SX; x++) {
				if (!compact) {
					sb.Append (" ");
				}
				var c = cs [x + y * grid.SX];
				if (grid.IsClosedAt (x, y)) {
					Debug.Assert (c == 0);
					// TODO: zelle kann piece enthalten
					c = compact ? '#' : '-';
				}
				else if (c == 0) {
					c = '.';
				}
				sb.Append (c);
			}
		}

		return sb.ToString ();
	}
}
