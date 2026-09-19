using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Minotaur.Utils;

public static class Util
{
	public static Color hslToRgb (double h, double s, double l)
	{
		// adapted from https://stackoverflow.com/a/29316972/39590
		double r, g, b;

		if (s == 0.0) {
			r = g = b = l; // achromatic
		}
		else {
			double hueToRgb (double p, double q, double t)
			{
				if (t < 0.0)
					t += 1.0;
				if (t > 1.0)
					t -= 1.0;
				if (t < 1.0 / 6.0)
					return p + (q - p) * 6.0 * t;
				if (t < 0.5)
					return q;
				if (t < 2.0 / 3.0)
					return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
				return p;
			}

			var q = l < 0.5 ? l * (1.0 + s) : l + s - l * s;
			var p = 2 * l - q;
			r = hueToRgb (p, q, h + 1.0 / 3.0);
			g = hueToRgb (p, q, h);
			b = hueToRgb (p, q, h - 1.0 / 3.0);
		}

		int to255 (double v)
		{
			return (int) Math.Min (255, 256 * v);
		}
		return Color.FromArgb (to255 (r), to255 (g), to255 (b));
	}

	public static readonly ThreadLocal<Random> ThreadRandom =
		new (() => new Random (HashCode.Combine (
			Thread.CurrentThread.ManagedThreadId,
			DateTime.UtcNow)));

	public static void Shuffle<T> (this IList<T> list)
	{
		var r = ThreadRandom.Value;
		int n = list.Count;
		while (n > 1) {
			n--;
			int k = r.Next (n + 1);
			T value = list [k];
			list [k] = list [n];
			list [n] = value;
		}
	}

	public static List<T> Shuffled<T> (this IEnumerable<T> source)
	{
		var list = source.ToList ();
		list.Shuffle ();
		return list;
	}

	static Util ()
	{
		for (int a = 0; a < 200; a++) {
			for (int b = 0; b < 100; b++) {
				var key = (((ulong) a) << 32) + (ulong) b;
				var val = BigInteger.One;
				for (int i = a + 1; i <= a + b; i++) {
					val *= i;
				}
				for (int i = 2; i <= b; i++) {
					val /= i;
				}
				_Fac3cache.Add (key, val);
			}
		}
	}

	private static Dictionary<ulong, BigInteger> _Fac3cache = new ();

	/// <summary>
	/// Calculates (a+b)! / (a! * b!)
	/// </summary>
	public static BigInteger Fac3 (int a, int b)
	{
		if (a < 0) {
			throw new ArgumentOutOfRangeException (nameof (a));
		}
		if (b < 0) {
			throw new ArgumentOutOfRangeException (nameof (b));
		}

		if (a < b) {
			var t = a;
			a = b;
			b = t;
		}

		var key = (((ulong) a) << 32) + (ulong) b;
		if (!_Fac3cache.TryGetValue (key, out var val)) {
			throw new InvalidProgramException ("Missing Fac3 value. Please increase the range in the static ctor.");
		}
		return val;
	}

	/// <summary>
	/// Calculates (n choose k)
	/// </summary>
	public static BigInteger Choose (int n, int k)
	{
		if (n < 0) {
			throw new ArgumentOutOfRangeException (nameof (n));
		}
		if (k < 0) {
			return 0;
		}
		if (n < k) {
			return 0;
		}

		return Fac3 (k, n - k);
	}

	/// <summary>
	/// Calculates n!
	/// </summary>
	public static BigInteger Fac (int n)
	{
		if (n < 0) {
			throw new ArgumentOutOfRangeException (nameof (n));
		}

		var c = BigInteger.One;
		for (int i = 1; i <= n; i++) {
			c *= i;
		}
		return c;
	}

	public static string BitsAsString (this uint u, int length)
	{
		if (length < 0) {
			throw new ArgumentOutOfRangeException (nameof (length), "Length must not be negative");
		}
		if (u >= (1u << length)) {
			throw new ArgumentOutOfRangeException (nameof (u), $"Integer {u} does not fit into range of {length} bits");
		}

		var sb = new StringBuilder (length);
		for (int i = length - 1; i >= 0; i--) {
			var b = u & (1u << i);
			sb.Append (b == 0 ? '0' : '1');
		}
		return sb.ToString ();
	}

	public static string Int2Base26 (int u)
	{
		if (u < 0) {
			throw new NotImplementedException ();
		}

		var sb = new StringBuilder ();
		do {
			var rem = u % 26;
			u = u / 26;
			sb.Append ((char) ('A' + rem));
		}
		while (u > 0);
		return sb.ToString ();
	}
}
