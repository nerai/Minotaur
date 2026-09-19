using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Minotaur.Utils;

public static class BalancedPermutation
{
	public static void Selftest ()
	{
		void test (int k, int n)
		{
			var rng = new Random (42);
			Console.WriteLine ($"=== K={k}, N={n} ===");
			var perms = GenerateBalancedPermutations (k, n, rng);
			foreach (var perm in perms) {
				Console.WriteLine (string.Join (" ", perm));
			}
			Console.WriteLine ();
		}

		test (1, 4);
		test (2, 4);
		test (3, 4);
		test (4, 4);
		test (5, 4);
	}

	/// <summary>
	/// Generates K permutations of N items (0-indexed).
	/// </summary>
	public static int [] [] GenerateBalancedPermutations (int k, int n, Random rng)
	{
		var candidates = new List<int []> ();
		for (int i = 0; i < 2 * n + 100; i++) {
			var order = Enumerable.Range (0, k).ToArray ();
			Shuffle (order, 0, k, rng);
			candidates.Add (order);
		}

		var selected = new List<int []> ();
		selected.Add (candidates [0]);
		candidates.RemoveAt (0);

		while (selected.Count < n) {
			var bestCandidateIndex = -1;
			var bestScore = double.NegativeInfinity;

			for (int i = 0; i < candidates.Count; i++) {
				var minScore = double.PositiveInfinity;
				foreach (var s in selected) {
					var d = ConflictScore (candidates [i], s);
					//Console.WriteLine ("Test " + string.Join (" ", candidates [i]) + " vs " + string.Join (" ", s) + " = " + d);
					if (d < minScore) {
						minScore = d;
					}
				}
				if (minScore > bestScore) {
					bestScore = minScore;
					bestCandidateIndex = i;
				}
			}

			//Console.WriteLine ($"Score {bestScore:0.0} add " + string.Join (" ", candidates [bestCandidateIndex]));
			selected.Add (candidates [bestCandidateIndex]);
			candidates.RemoveAt (bestCandidateIndex);
		}

		return selected.ToArray ();
	}

	static void Shuffle (int [] array, int start, int count, Random rng)
	{
		for (int i = count - 1; i > 0; i--) {
			int j = rng.Next (i + 1);
			(array [start + i], array [start + j]) = (array [start + j], array [start + i]);
		}
	}

	static double ConflictScore (
		int [] a,
		int [] b,
		double positionWeight = 2.0,
		bool countReverseBlocks = false)
	{
		if (a.Length != b.Length)
			throw new ArgumentException ("Permutations must have the same length.");

		int k = a.Length;

		var positionInA = new Dictionary<int, int> ();

		for (int i = 0; i < k; i++)
			positionInA [a [i]] = i;

		double score = a.Length;

		// 1. Same-index conflicts.
		for (int i = 0; i < k; i++) {
			if (a [i] == b [i])
				score -= positionWeight;
		}

		// 2. Convert b into positions in a.
		int [] mapped = new int [k];

		for (int i = 0; i < k; i++)
			mapped [i] = positionInA [b [i]];

		// 3. Count same-order contiguous repeated blocks.
		score -= ContiguousRunPenalty (mapped, direction: +1);

		// Optional: also penalize reversed repeated blocks.
		// Example: 1234 vs 4321 contains reversed block 4321.
		if (countReverseBlocks)
			score -= 0.1 * ContiguousRunPenalty (mapped, direction: -1);

		return score;
	}

	private static double ContiguousRunPenalty (int [] mapped, int direction)
	{
		static double RunPenalty (int runLength)
		{
			if (runLength < 2)
				return 0.0;

			double score = 0.0;

			// Every run of length r contains:
			// r - 1 blocks of length 2
			// r - 2 blocks of length 3
			// ...
			// 1 block of length r
			for (int blockLength = 2; blockLength <= runLength; blockLength++) {
				int occurrences = runLength - blockLength + 1;

				double weight = BlockWeight (blockLength);

				score += occurrences * weight;
			}

			return score;
		}
		static double BlockWeight (int blockLength)
		{
			// Mildly increasing penalty.
			return blockLength - 1;

			// Alternative, stronger:
			// return blockLength * blockLength;

			// Alternative, very strong:
			// return Math.Pow(2, blockLength - 2);
		}

		double score = 0.0;
		int runLength = 1;
		for (int i = 1; i < mapped.Length; i++) {
			if (mapped [i] == mapped [i - 1] + direction) {
				runLength++;
			}
			else {
				score += RunPenalty (runLength);
				runLength = 1;
			}
		}
		score += RunPenalty (runLength);
		return score;
	}

}
