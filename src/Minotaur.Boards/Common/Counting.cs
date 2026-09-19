using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Minotaur.Utils;

namespace Minotaur.Boards.Common;

public static class Counting
{
	public static void TestCanonicalBoardCount ()
	{
		for (int X = 1; X <= 10; X++) {
			for (int Y = 1; Y <= X; Y++) {
				for (int k = 0; k <= X * Y; k++) {
					var f = CanonicalBoardCount (Y, X, k);
					if (k > 0 && k % 5 == 0) {
						Console.WriteLine ($"{X}x{Y}/{k / 5}: {f}");
					}
					else {
						Console.WriteLine ($"{X}x{Y},{k}b: {f}");
					}
				}
			}
		}
	}

	/// <summary>
	/// Number of two-colored rectangular boards, with k cells of one color, without duplicates per rotation and/or reflection.
	/// </summary>
	/// <param name="Y">
	/// Y Size of rectangle
	/// </param>
	/// <param name="X">
	/// X Size of rectangle. Must be >= Y.
	/// </param>
	/// <param name="k">
	/// Number of cells in one of the colors.
	/// Must be between 0 and X * Y.
	/// </param>
	public static BigInteger CanonicalBoardCount (int Y, int X, int k)
	{
		if (X == Y) {
			return CanonicalBoardCount_Square (X, k);
		}
		else {
			return CanonicalBoardCount_Rect (Y, X, k);
		}
	}

	private static BigInteger CanonicalBoardCount_Square (int N, int k)
	{
		if (N < 0) {
			throw new ArgumentOutOfRangeException (nameof (N));
		}
		if (k < 0) {
			throw new ArgumentOutOfRangeException (nameof (k));
		}

		var A = Util.Choose (N * N, k);
		var C = Util.Choose (N * N / 2, k / 2);
		var D = Util.Choose (N * N / 4, k / 4);

		var B = BigInteger.Zero;
		for (int i = 0; i <= k; i++) {
			if (i % 2 != k % 2) {
				continue;
			}
			B += Util.Choose (N, i) * Util.Choose ((N * N - N) / 2, (k - i) / 2);
		}

		var option = (1 - N % 2, k % 4);
		BigInteger f;

		switch (option) {
			case (1, 0):
				f = A + 2 * B + 3 * C + 2 * D;
				break;
			case (1, 1):
			case (1, 3):
				f = A + 2 * B;
				break;
			case (1, 2):
				f = A + 2 * B + 3 * C;
				break;
			case (0, 0):
			case (0, 1):
				f = A + 4 * B + C + 2 * D;
				break;
			case (0, 2):
			case (0, 3):
				f = A + 4 * B + C;
				break;
			default:
				throw new InvalidProgramException ();
		}

		if (!f.IsEven || !(f / 2).IsEven || !(f / 4).IsEven) {
			throw new InvalidProgramException ($"Illegal value, not divisible by 8. {N}x{N},{k}b: {f}");
		}
		f /= 8;

		return f;
	}

	private static BigInteger CanonicalBoardCount_Rect (int Y, int X, int k)
	{
		if (Y < 0) {
			throw new ArgumentOutOfRangeException (nameof (Y));
		}
		if (X < 0) {
			throw new ArgumentOutOfRangeException (nameof (X));
		}
		if (k < 0) {
			throw new ArgumentOutOfRangeException (nameof (k));
		}

		var A = Util.Choose (Y * X, k);
		var B = Util.Choose (Y * X / 2, k / 2);

		var C = BigInteger.Zero;
		var D = BigInteger.Zero;
		for (int i = 0; i <= k; i++) {
			if (i % 2 != k % 2) {
				continue;
			}
			C += Util.Choose (Y, i) * Util.Choose ((X - 1) * Y / 2, (k - i) / 2);
			D += Util.Choose (X, i) * Util.Choose ((Y - 1) * X / 2, (k - i) / 2);
		}

		var option = (1 - Y % 2, 1 - X % 2, 1 - k % 2); // Are M, N, k even?
		BigInteger f;
		switch (option) {
			case (1, 1, 1):
				f = A + 3 * B;
				break;
			case (1, 1, 0):
				f = A;
				break;
			case (1, 0, 1):
				f = A + 2 * B + C;
				break;
			case (1, 0, 0):
				f = A + C;
				break;
			case (0, 1, 1):
				f = A + 2 * B + D;
				break;
			case (0, 1, 0):
				f = A + D;
				break;
			case (0, 0, 1):
				var E = Util.Choose ((Y * X - 1) / 2, k / 2);
				f = A + C + D + E;
				break;
			case (0, 0, 0):
				var F = Util.Choose ((Y * X - 1) / 2, (k - 1) / 2);
				f = A + C + D + F;
				break;
			default:
				throw new InvalidProgramException ();
		}

		if (!f.IsEven || !(f / 2).IsEven) {
			throw new InvalidProgramException ($"Illegal value, not divisible by 4. {X}x{Y},{k}b: {f}");
		}
		f /= 4;

		return f;
	}
}
