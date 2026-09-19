using System;
using System.Diagnostics;
using System.Linq;
using Minotaur.Boards.Common;
using Minotaur.GenericTreeSearch;
using Minotaur.Utils;

namespace Minotaur.Boards.Strings;

public sealed class WormyTreeCreationMeta : IExpandMeta
{
	public WormyTreeCreationMeta ()
	{
	}

	public WormyPool Pool { get; } = new WormyPool ();

	public long ForciblyPruneAfterVisits { get; set; } = long.MaxValue;

	public bool PruneImmediately { get; set; } = false;

	public bool checkEndCondition = true;
	public bool allowBottlenecks = true;
	public bool allowBoardsplit = true;
	public bool allowChoices = true;
	public bool allowDecisions = true;
	public bool allowPieceOperation = true;
	public bool prefilterDecisions = true;

	// Scaled by factor 5 so we can use integers
	public SaturatingCost Cost_Bottleneck = 5u;
	public SaturatingCost Cost_Choice = 15u;
	public SaturatingCost Cost_Decision = 16u;
	public SaturatingCost Cost_Boardsplit = 17u;
	public SaturatingCost Cost_Piece = 51u;

	public bool Desire_IncludeReachable2 = true;

	/*
	 * Names of parameters:
	 * 
	 * P: pivot, --or-- (in decisions) larger of two strings
	 * N: if present (in decisions): smaller of two strings
	 * 0,1,2: Distance from that string(s)
	 * 
	 * S: Number of strings
	 * C: Number of cells
	 * A: Average cells per string
	 * Ht: Hotness
	 * 
	 * Consequence:
	 * x0_S is always 1.
	 * x0_C is always the number of cells in the string.
	 */

	public readonly double BaseDesireForBottleneck = 500;

	/*
	 * These values were trained slightly further than in the paper.
	 * Their direction is still practically the same.
	 */
	public double Desire_Choice_Base = -763.937061;
	public double Desire_Choice_P_Ht = 2.398157;
	public double Desire_Choice_P0_C = 5.507772;

	public double Desire_Choice_P1_A = 2.838817;
	public double Desire_Choice_P1_C = 0.691910;
	public double Desire_Choice_P1_S = -0.581122;

	public double Desire_Choice_P2_A = -0.426285;
	public double Desire_Choice_P2_C = -0.190620;
	public double Desire_Choice_P2_S = 1.782005;

	public double Desire_Decision_Base = -1804.854399;
	public double Desire_Decision_N_Ht = -2.891773;
	public double Desire_Decision_N0_C = 9.665577;

	public double Desire_Decision_N1_A = 5.901879;
	public double Desire_Decision_N1_C = 7.950741;
	public double Desire_Decision_N1_S = -9.663855;

	public double Desire_Decision_N2_A = -4.963903;
	public double Desire_Decision_N2_C = -5.258798;
	public double Desire_Decision_N2_S = -2.429315;

	public double Desire_Decision_P_Ht = -0.060725;
	public double Desire_Decision_P0_C = 8.934378;

	public double Desire_Decision_P1_A = -1.067686;
	public double Desire_Decision_P1_C = -1.031224;
	public double Desire_Decision_P1_S = -4.777983;

	public double Desire_Decision_P2_A = -4.642540;
	public double Desire_Decision_P2_C = -0.247826;
	public double Desire_Decision_P2_S = -5.432602;

	public readonly double Desire_Piece_Base = -500;
	public double Desire_Piece_Range = 199.565297;
	// Penalty for finished cell count
	public double Desire_Piece_Cells = 64.692990;
	// Penalry for having many options to insert the piece
	public double Desire_Piece_Options = -51.892100;

	public void SetParams (double [] values)
	{
		int i = 0;

		//BaseDesireForBottleneck = values [i++];

		Desire_Choice_Base = values [i++];
		Desire_Choice_P_Ht = values [i++];
		Desire_Choice_P0_C = values [i++];

		Desire_Choice_P1_A = values [i++];
		Desire_Choice_P1_C = values [i++];
		Desire_Choice_P1_S = values [i++];

		Desire_Choice_P2_A = values [i++];
		Desire_Choice_P2_C = values [i++];
		Desire_Choice_P2_S = values [i++];

		Desire_Decision_Base = values [i++];
		Desire_Decision_N_Ht = values [i++];
		Desire_Decision_N0_C = values [i++];

		Desire_Decision_N1_A = values [i++];
		Desire_Decision_N1_C = values [i++];
		Desire_Decision_N1_S = values [i++];

		Desire_Decision_N2_A = values [i++];
		Desire_Decision_N2_C = values [i++];
		Desire_Decision_N2_S = values [i++];

		Desire_Decision_P_Ht = values [i++];
		Desire_Decision_P0_C = values [i++];

		Desire_Decision_P1_A = values [i++];
		Desire_Decision_P1_C = values [i++];
		Desire_Decision_P1_S = values [i++];

		Desire_Decision_P2_A = values [i++];
		Desire_Decision_P2_C = values [i++];
		Desire_Decision_P2_S = values [i++];

		//Desire_Piece_Base = values [i++];
		Desire_Piece_Range = values [i++];
		Desire_Piece_Cells = values [i++];
		Desire_Piece_Options = values [i++];
	}

	public IEnumerable<double> GetParams ()
	{
		//yield return BaseDesireForBottleneck;

		yield return Desire_Choice_Base;
		yield return Desire_Choice_P_Ht;
		yield return Desire_Choice_P0_C;

		yield return Desire_Choice_P1_A;
		yield return Desire_Choice_P1_C;
		yield return Desire_Choice_P1_S;

		yield return Desire_Choice_P2_A;
		yield return Desire_Choice_P2_C;
		yield return Desire_Choice_P2_S;

		yield return Desire_Decision_Base;
		yield return Desire_Decision_N_Ht;
		yield return Desire_Decision_N0_C;

		yield return Desire_Decision_N1_A;
		yield return Desire_Decision_N1_C;
		yield return Desire_Decision_N1_S;

		yield return Desire_Decision_N2_A;
		yield return Desire_Decision_N2_C;
		yield return Desire_Decision_N2_S;

		yield return Desire_Decision_P_Ht;
		yield return Desire_Decision_P0_C;

		yield return Desire_Decision_P1_A;
		yield return Desire_Decision_P1_C;
		yield return Desire_Decision_P1_S;

		yield return Desire_Decision_P2_A;
		yield return Desire_Decision_P2_C;
		yield return Desire_Decision_P2_S;

		//yield return Desire_Piece_Base;
		yield return Desire_Piece_Range;
		yield return Desire_Piece_Cells;
		yield return Desire_Piece_Options;
	}

	public string LogHeader ()
	{
		return "Date"

			+ "\tCost"
			+ "\tGrid"

			//+ "\t" + nameof (BaseDesireForBottleneck)

			+ "\t" + nameof (Desire_Choice_Base)
			+ "\t" + nameof (Desire_Choice_P_Ht)
			+ "\t" + nameof (Desire_Choice_P0_C)

			+ "\t" + nameof (Desire_Choice_P1_A)
			+ "\t" + nameof (Desire_Choice_P1_C)
			+ "\t" + nameof (Desire_Choice_P1_S)

			+ "\t" + nameof (Desire_Choice_P2_A)
			+ "\t" + nameof (Desire_Choice_P2_C)
			+ "\t" + nameof (Desire_Choice_P2_S)

			+ "\t" + nameof (Desire_Decision_Base)
			+ "\t" + nameof (Desire_Decision_N_Ht)
			+ "\t" + nameof (Desire_Decision_N0_C)

			+ "\t" + nameof (Desire_Decision_N1_A)
			+ "\t" + nameof (Desire_Decision_N1_C)
			+ "\t" + nameof (Desire_Decision_N1_S)

			+ "\t" + nameof (Desire_Decision_N2_A)
			+ "\t" + nameof (Desire_Decision_N2_C)
			+ "\t" + nameof (Desire_Decision_N2_S)

			+ "\t" + nameof (Desire_Decision_P_Ht)
			+ "\t" + nameof (Desire_Decision_P0_C)

			+ "\t" + nameof (Desire_Decision_P1_A)
			+ "\t" + nameof (Desire_Decision_P1_C)
			+ "\t" + nameof (Desire_Decision_P1_S)

			+ "\t" + nameof (Desire_Decision_P2_A)
			+ "\t" + nameof (Desire_Decision_P2_C)
			+ "\t" + nameof (Desire_Decision_P2_S)

			//+ "\t" + nameof (Desire_Piece_Base)
			+ "\t" + nameof (Desire_Piece_Range)
			+ "\t" + nameof (Desire_Piece_Cells)
			+ "\t" + nameof (Desire_Piece_Options)

			+ "\n";
	}

	public string LogRow_OLD_WRONGFORMAT (string grid, long nodes, ulong cost)
	{
		return $"{DateTime.UtcNow:yyyy.MM.dd HH.mm.ss}"

			/*
			+ $"\t{Cost_Bottleneck.AsUlong ()}"
			+ $"\t{Cost_Boardsplit.AsUlong ()}"
			+ $"\t{Cost_Choice.AsUlong ()}"
			+ $"\t{Cost_Decision.AsUlong ()}"
			+ $"\t{Cost_Piece.AsUlong ()}"
			*/
			//+ $"\t{Desire_IncludeReachable2}"

			+ "\t" + $"{Desire_Choice_Base:0.0000}"
			+ "\t" + $"{Desire_Choice_P_Ht:0.0000}"
			+ "\t" + $"{Desire_Choice_P0_C:0.0000}"

			+ "\t" + $"{Desire_Choice_P1_A:0.0000}"
			+ "\t" + $"{Desire_Choice_P1_C:0.0000}"
			+ "\t" + $"{Desire_Choice_P1_S:0.0000}"

			+ "\t" + $"{Desire_Choice_P2_A:0.0000}"
			+ "\t" + $"{Desire_Choice_P2_C:0.0000}"
			+ "\t" + $"{Desire_Choice_P2_S:0.0000}"

			+ "\t" + $"{Desire_Decision_Base:0.0000}"
			+ "\t" + $"{Desire_Decision_N_Ht:0.0000}"
			+ "\t" + $"{Desire_Decision_N0_C:0.0000}"

			+ "\t" + $"{Desire_Decision_N1_A:0.0000}"
			+ "\t" + $"{Desire_Decision_N1_C:0.0000}"
			+ "\t" + $"{Desire_Decision_N1_S:0.0000}"

			+ "\t" + $"{Desire_Decision_N2_A:0.0000}"
			+ "\t" + $"{Desire_Decision_N2_C:0.000}"
			+ "\t" + $"{Desire_Decision_N2_S:0.0000}"

			+ "\t" + $"{Desire_Decision_P_Ht:0.0000}"
			+ "\t" + $"{Desire_Decision_P0_C:0.0000}"

			+ "\t" + $"{Desire_Decision_P1_A:0.0000}"
			+ "\t" + $"{Desire_Decision_P1_C:0.0000}"
			+ "\t" + $"{Desire_Decision_P1_S:0.0000}"

			+ "\t" + $"{Desire_Decision_P2_A:0.0000}"
			+ "\t" + $"{Desire_Decision_P2_C:0.0000}"
			+ "\t" + $"{Desire_Decision_P2_S:0.0000}"

			+ "\t" + $"{Desire_Piece_Base:0.0000}"
			+ "\t" + $"{Desire_Piece_Range:0.0000}"

			+ $"\t{grid}"
			+ $"\t{nodes}"
			+ $"\t{cost}"

			+ "\n";
	}
}
