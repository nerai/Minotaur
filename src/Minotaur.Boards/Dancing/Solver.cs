using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Minotaur.Boards.Common;
using Minotaur.ExactCover;
using Minotaur.Utils;
using Newtonsoft.Json.Linq;

using static Minotaur.Utils.Logging;

namespace Minotaur.Boards.Dancing;

public class Solver
{
	private IReadOnlyList<Piece> _MovablePieces;
	private IReadOnlyList<Piece> _StaticPieces;
	public readonly BoardGrid Grid;

	public LinkedMatrixTranslate Matrix { get; private set; }

	public int Steps => Matrix.Steps;
	public int StepsToFirst => Matrix.StepsToFirst;

	public List<List<Placement>> Solutions;

	public DancingSolutionNode TreeRoot = null;

	public Solver (IReadOnlyList<Piece> allPieces, JObject json)
	{
		_MovablePieces = json
			.SelectToken ("Board")
			.SelectToken ("Pieces")
			.Value<string> ()
			.Split (',').Select (name => allPieces.Single (p => p.Name == name))
			.ToArray ();

		_StaticPieces = new Piece [0];

		if (json.SelectToken ("Rotations") == null) {
			// old version
			Grid = new BoardGrid (json.SelectToken ("Board").SelectToken ("Closed").Value<string> ());
		}
		else {
			var b = json.SelectToken ("Rotations").Children ().First ();
			var x = b.SelectToken ("X").Value<int> ();
			var y = b.SelectToken ("Y").Value<int> ();
			var closed = b.SelectToken ("Closed").Value<string> ();
			Grid = new BoardGrid (x, y, closed);
		}
	}

	public Solver (string boardHash, IReadOnlyList<Piece> movablePieces = null)
	{
		_MovablePieces = movablePieces ?? Piece.AllPieces;
		_StaticPieces = Array.Empty<Piece> ();

		Grid = new BoardGrid (boardHash);
	}

	public Solver (IReadOnlyList<Piece> movables, IReadOnlyList<Piece> statics, BoardGrid grid)
	{
		_MovablePieces = movables;
		_StaticPieces = statics;
		Grid = grid;
	}

	private static readonly SortedSet<(int size, long _, Stack<QLCell> stack)> _Pools = new ();
	private static int _LentPools = 0;
	private static long _TaskCounter = 0;

	private const bool DebugPrintLentPools = false;

	// Solve current board without moving pieces
	public bool Solve (
		bool createSolutionTree,
		bool exploreOnlyOneColumn = true,
		bool randomize = false,
		ulong [] possibleInsertLocationsHistogram = null)
	{
		//LogN ($"Solver started with {_MovablePieces.Count} movable and {_StaticPieces.Count} static pieces");
		//MeasurePerformance (null);

		Stack<QLCell> pool = null;
		lock (_Pools) {
			if (_Pools.Count > 0) {
				var max = _Pools.Max;
				_Pools.Remove (max);
				pool = max.stack;
			}
		}
		if (DebugPrintLentPools) {
			var lp = Interlocked.Increment (ref _LentPools);
			if (pool == null) {
				LogN ($"Creating new QLC pool, lent {_LentPools}");
			}
			else {
				LogN ($"Reusing existing QLC pool ({pool.Count} cells), lent {lp}");
			}
		}
		if (pool == null) {
			pool = new Stack<QLCell> ();

		}

		Matrix = new LinkedMatrixTranslate (
			Grid,
			_MovablePieces,
			pool,
			exploreOnlyOneColumn,
			randomize,
			possibleInsertLocationsHistogram,
			createTree: createSolutionTree);

		Solutions = Matrix.GetSolutions ();
		//Console.WriteLine ($"Got {Solutions.Count} solutions");

		if (createSolutionTree) {
			TreeRoot = Matrix.TranslateEntireSolutionTree ();
		}

		if (DebugPrintLentPools) {
			var lp = Interlocked.Decrement (ref _LentPools);
			LogN ($"Returning QLC pool ({pool.Count} cells), lent {lp}");
		}
		{
			var add = (pool.Count, Interlocked.Increment (ref _TaskCounter), pool);
			lock (_Pools) {
				_Pools.Add (add);
			}
		}

		return Solutions.Count > 0;
	}

	public static Solver Solved (string hash)
	{
		var s = new Solver (hash);
		s.Solve (false);
		return s;
	}

	public string Description {
		get {
			var logstepsT = Math.Log (Steps, 2);
			var logsteps0 = Math.Log (StepsToFirst, 2);
			var group = $"{Grid.SX}x{Grid.SY}, P={Grid.OpenTileCount / 5}, Sol={Solutions.Count:0}, StT={logstepsT:0.0}, St0={logsteps0:0.0}";
			return group;
		}
	}

	public static List<long> RandomizedStepsForGrid (string grid)
	{
		var steps = new List<long> ();
		var indexes = Enumerable.Range (0, 100);
		CustomParallel.ForEach (
			$"Solver::RandomizedStepsForGrid {grid}",
			Environment.ProcessorCount,
			"Solver::RandomizedStepsForGrid",
			(uint) Environment.ProcessorCount,
			0,
			ThreadPriority.BelowNormal,
			10.0,
			indexes,
			i => {
				var ss = new Solver (grid);
				ss.Solve (false, true, true);
				lock (steps) {
					steps.Add (ss.Steps);
				}
			});
		return steps;
	}
}
