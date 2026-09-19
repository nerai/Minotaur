using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Minotaur.Utils;

namespace Minotaur.ExactCover;

/// <summary>
/// Creates a log from the solving process of a LinkedMatrix.
/// 
/// Integrated with the solving process: It can detect duplicate nodes.
/// </summary>
public class LinkedMatrixTreeCreator
{
	private readonly LinkedMatrix _M;
	public readonly LinkedMatrixTreeNode Root;

	private LinkedMatrixTreeNode _Cur;

	/// <summary>
	/// Zobrist hash over all rows that are active.
	/// This uniquely identifies a board position.
	/// </summary>
	private UInt64 _Zobrist;

	public readonly List<List<List<QLCell>>> Solutions = new List<List<List<QLCell>>> ();

	public int Steps { get; private set; } = 0;
	public int StepsToFirst { get; private set; } = -1;

	/// <summary>
	/// If false, no branches will be created that cover the same cells using the same pieces as another, earlier branch.
	/// The solvability of such branches is exactly the same.
	/// However, the board configuration is not neccessarily the same - e.g. a part of the board may be rotated.
	/// 
	/// Set to false if you just want to know if this is solvable.
	/// Set to true if you want to build the whole tree with all board configurations.
	/// 
	/// ACHTUNG: dafuer noetig ist anpassung von zob hash
	/// diese idee hier IST kompatibel mit zobrist hash WENN man den hash speziell erstellt.
	/// und zwar muss man jeder ZEILE den hash zuweisen als xor aller ihrer verwendeten SPALTEN.
	/// also nicht einfach eine zufallszahl pro zeile.
	/// </summary>
	public bool AllowEquivalentBranches = false;

	private readonly Dictionary<UInt64, List<LinkedMatrixTreeNode>> _Known;

	public LinkedMatrixTreeCreator (
		LinkedMatrix m,
		bool checkRepetitions,
		bool createTree)
	{
		//Console.WriteLine ($"\nLMTC ctor");
		_M = m ?? throw new ArgumentNullException (nameof (m));

		_M.SolutionFound = SolutionFound;

		if (createTree) {
			Root = new LinkedMatrixTreeNode ();
			_M.EnterColumn = EnterColumn;
			_M.LeaveColumn = LeaveColumn;
			_M.EnterRow = EnterRow;
			_M.LeaveRow = LeaveRow;
		}
		else {
			Root = null;
			void enterRow (QLCell row, out bool allowed)
			{
				allowed = true;
				Steps++;
			}
			_M.EnterRow = enterRow;
		}
		_Cur = Root;

		if (checkRepetitions) {
			if (!createTree) {
				throw new ArgumentException ("To check repetitions, a tree must be created.");
			}
			_Known = new ();
		}
		else {
			_Known = null;
		}
	}

	private void EnterColumn (ColHead col, int worseThanBest)
	{
		//Console.WriteLine ($"Enter col {col}");
		var next = new LinkedMatrixTreeNode (_Cur, col, worseThanBest);
		_Cur.Children.Add (next);
		_Cur = next;
	}

	private void LeaveColumn (ColHead col)
	{
		//Console.WriteLine ($"Leave col {col}: {_Cur} -> {_Cur.Parent}");
		_Cur = _Cur.Parent;
	}

	private void EnterRow (QLCell row, out bool allowed)
	{
		//Console.WriteLine ($"Enter row {row}");
		var next = new LinkedMatrixTreeNode (_Cur, row);

		if (_Known != null) {
			/*
			 * Check if we need to enter this branch: Did we see it before?
			 * Compare using (in order):
			 * - the Zobrist hash (extremely fast, depends on columns, is probabilistic)
			 * - the set of used rows (very fast, depends on rows, is precise)
			 * Note that we NEVER compare a board grid. That would be slow.
			 */
			_Zobrist ^= Hashing.Hash64 (row.RowIndex);
			if (!_Known.TryGetValue (_Zobrist, out var similarNodes)) {
				similarNodes = new List<LinkedMatrixTreeNode> ();
				_Known.Add (_Zobrist, similarNodes);
			}
			else {
				/*
				 * This hash was seen before. Does it match the board?
				 * Note that this depends on the AllowEquivalentBranches setting.
				 */
				if (AllowEquivalentBranches) {
					// TODO compare using columns (not rows)
					throw new NotImplementedException ();
				}
				else {
					// TODO: je nachdem wie teuer das hier ist, vielleicht besser von anfang an in ein immutable byte array umwandeln und damit vergleichen. siehe alten DancingSolutionCreator.
					// TODO: je nachdem wie gut zob hash geht braucht man das hier nicht zu testen
					var rowsUsedByBranch = next.GetAllUsedRows ().ToHashSet ();
					foreach (var similar in similarNodes) {
						var hasSameRows = true;
						foreach (var usedRow in similar.GetAllUsedRows ()) {
							if (!rowsUsedByBranch.Contains (usedRow)) {
								hasSameRows = false;
								break;
							}
						}
						if (hasSameRows) {
							allowed = false;
							throw new NotImplementedException ("todo Steps anpassen");
							return;
						}
					}
				}
			}

			/*
			 * This branch is new.
			 * Remember it.
			 */
			similarNodes.Add (next);
		}

		/*
		 * Perform the transition
		 */
		allowed = true;
		_Cur.Children.Add (next);
		_Cur = next;
		Steps++;
	}

	private void LeaveRow (QLCell row)
	{
		if (_Known != null) {
			_Zobrist ^= Hashing.Hash64 (row.RowIndex);
		}

		//Console.WriteLine ($"Leave row {row}: {_Cur} -> {_Cur.Parent}");
		_Cur = _Cur.Parent;
	}

	private bool SolutionFound (Stack<QLCell> path)
	{
		//Console.WriteLine ($"Solution found {string.Join (",", path)}");
		if (StepsToFirst <= 0) {
			StepsToFirst = Steps;
		}

		var solution = new List<List<QLCell>> (path.Count);
		foreach (var node in path) {
			var row = node.EnumerateRow ().ToList ();
			solution.Add (row);
		}
		Solutions.Add (solution);

		return true;
	}
}
