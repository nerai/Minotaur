using System;

namespace Minotaur.Explanation;

public static class LatexExtensions
{
	public static string EscapeLatex (this string s)
	{
		if (s.Contains ("\n")) {
			throw new ArgumentException ("Newlines must be translated manually");
		}

		return s
			.Replace ("&", "\0&")
			.Replace ("%", "\0%")
			.Replace ("$", "\0$")
			.Replace ("#", "\0#")
			.Replace ("_", "\0_")
			.Replace ("{", "\0{")
			.Replace ("}", "\0}")
			.Replace ("~", "\0textasciitilde")
			.Replace ("^", "\0textasciicircum")
			.Replace ("\\", "\0textbackslash")
			.Replace ("\0", "\\")
			;
		// TODO non-ascii als unicode?!
	}
}
