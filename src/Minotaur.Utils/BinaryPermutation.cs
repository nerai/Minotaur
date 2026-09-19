using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Minotaur.Utils;

public static class BinaryPermutation
{
	public static IEnumerable<BigInteger> BinaryPermutationsSlow (int ones, int zeros, BigInteger? startAt = null)
	{
		if (ones < 0) {
			throw new ArgumentOutOfRangeException (nameof (ones));
		}
		if (zeros < 0) {
			throw new ArgumentOutOfRangeException (nameof (zeros));
		}

		var end = (BigInteger.One << ones) - 1;
		var b = startAt ?? end;

		while (true) {
			//Console.WriteLine ($"v = {b} = {b.ToString ("X")}");
			yield return b;

			// generate next permutation
			var t = (b | (b - 1)) + 1;
			b = t | ((((t & -t) / (b & -b)) >> 1) - 1);

			if (b == end) {
				break;
			}
		}
	}

	public static SmallBitArray BI2BA (int bits, BigInteger bi)
	{
		throw new NotImplementedException ("ungetestet");
		var ba = new SmallBitArray (bits, false);
		int i = 0;
		while (bi > 0) {
			ba.Set (i, (bi & 1) != 0);
			bi >>= 1;
			i++;
		}
		return ba;
	}

	const int _PascalTriangleMaxBits = 260;
	private static readonly BigInteger [] [] _PascalTriangleBI;

	static BinaryPermutation ()
	{
		checked {
			_PascalTriangleBI = new BigInteger [_PascalTriangleMaxBits + 1] [];
			for (int i = 0; i <= _PascalTriangleMaxBits; ++i) {
				_PascalTriangleBI [i] = new BigInteger [i + 1];
				_PascalTriangleBI [i] [0] = 1;
				for (int j = 1; j < i; ++j) {
					_PascalTriangleBI [i] [j] = _PascalTriangleBI [i - 1] [j - 1] + _PascalTriangleBI [i - 1] [j];
				}
				_PascalTriangleBI [i] [i] = 1;
			}
		}
	}

	public static IEnumerable<SmallBitArray> BinaryPermutationsIndexed (int ones, int zeros, BigInteger skip)
	{
		if (ones < 0) {
			throw new ArgumentOutOfRangeException (nameof (ones));
		}
		if (zeros < 0) {
			throw new ArgumentOutOfRangeException (nameof (zeros));
		}

		var fac = Util.Fac3 (ones, zeros) - 1;
		var index = skip;
		var total = fac;
		while (index <= total) {
			var b = PermutationByIndex_NoChecks (index, ones, zeros);
			yield return b;
			index++;
		}
	}

	private static SmallBitArray PermutationByIndex_NoChecks (BigInteger index, int ones, int zeros)
	{
		Debug.Assert (ones >= 0);
		Debug.Assert (zeros >= 0);

		int row = ones + zeros;
		int col = zeros;
		Debug.Assert (row <= _PascalTriangleMaxBits);
		var res = new SmallBitArray (row);

		for (; ; ) {
			if (row == 0) {
				break;
			}
			--row;
			if (col > 0 && index < _PascalTriangleBI [row] [col - 1]) {
				--col;
			}
			else {
				if (col > 0) {
					index -= _PascalTriangleBI [row] [col - 1];
				}
				res.Set (row, true);
			}

		}
		return res;
	}

	public static BigInteger IndexByPermutation (SmallBitArray a)
	{
		int zeros = 0;
		for (int i = 0; i < a.Length; i++) {
			if (!a [i]) {
				zeros++;
			}
		}

		int row = a.Length;
		int col = zeros;
		Debug.Assert (row <= _PascalTriangleMaxBits);
		BigInteger index = 0;

		for (; ; ) {
			if (_PascalTriangleBI [row] [col] == 1) {
				break;
			}
			--row;
			if (a [row]) {
				index += _PascalTriangleBI [row] [col - 1];
			}
			else {
				col--;
			}
		}
		return index;
	}

	public static void TestSmallSample ()
	{
		var slow = BinaryPermutationsSlow (3, 5)
			.Take (100)
			.OrderBy (i => i)
			.ToList ();
		var fast = Enumerable.Range (0, slow.Count)
			.Select (i => PermutationByIndex_NoChecks ((ulong) i, 5, 3))
			.OrderBy (i => i)
			.ToList ();

		for (int i = 0; i < slow.Count; i++) {
			Console.WriteLine ($"{i,5} {slow [i],30}  {fast [i],30}");
		}
	}

	public static void TestBigSample ()
	{
		for (int bits = 1; bits <= _PascalTriangleMaxBits; bits = bits * 8 / 7 + 1) {
			for (int ones = 0; ones <= bits; ones = ones * 4 / 3 + 1) {
				int zeros = bits - ones;
				for (int where = 1; where <= 100_000; where *= 10) {
					if (where > Util.Fac3 (bits - ones, ones)) {
						break;
					}
					var slowRaw = BinaryPermutationsSlow (ones, bits - ones).Take (where).Last ();
					var slow = new SmallBitArray (slowRaw.ToByteArray ().Concat (new byte [20]).ToArray ());
					var sSlow = slow.ToString ();
					sSlow = sSlow.Substring (0, bits);

					var fast = PermutationByIndex_NoChecks ((ulong) where - 1, ones, zeros);
					var sFast = fast.ToString ();

					Console.WriteLine ($"{bits,3}  {ones,3}  {where,7}  {sSlow,30}  {sFast,30}");
					if (sSlow != sFast) {
						Debugger.Break ();
					}

					var iByP = IndexByPermutation (fast);
					if (iByP != where - 1) {
						Debugger.Break ();
					}
				}
			}
		}
	}

	public static void TestPerformance ()
	{
		const int n = 10_000_000;
		var write = new SmallBitArray [n];

		void TestOld ()
		{
			var e = BinaryPermutationsSlow (50, 50).GetEnumerator ();
			for (int i = 0; i < n; i++) {
				e.MoveNext ();
				write [i] = new SmallBitArray (e.Current.ToByteArray ());
			}
		}

		void TestNew ()
		{
			for (ulong i = 0; i < n; i++) {
				write [i] = PermutationByIndex_NoChecks (i, 50, 50);
			}
		}

		void Test (string s, Action a)
		{
			var sw = Stopwatch.StartNew ();
			a ();
			sw.Stop ();
			Console.WriteLine ($"{sw.Elapsed.TotalSeconds,5:0.0}s  {s}");
		}

		for (int i = 0; i < 3; i++) {
			Test ($"Old {i}", TestOld);
			Test ($"New {i}", TestNew);
		}
	}
}
