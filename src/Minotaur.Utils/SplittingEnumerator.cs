using System;

namespace Minotaur.Utils;

public class NoMoreElementsException : Exception
{
	public readonly string Line;

	public NoMoreElementsException (string message, string line)
		: base ($"{message} in \"{line}\"")
	{
		Line = line;
	}
}

public ref struct SplittingEnumerator
{
	private ReadOnlySpan<char> _Original;
	private ReadOnlySpan<char> _S;
	public readonly char Separator;
	public readonly bool RemoveEmpty;
	public readonly bool Trim;

	public ReadOnlySpan<char> Current { get; private set; }

	public SplittingEnumerator GetEnumerator () => this;

	public SplittingEnumerator (ReadOnlySpan<char> str, char separator, bool removeEmpty, bool trim)
	{
		_Original = str;
		_S = str;
		Separator = separator;
		RemoveEmpty = removeEmpty;
		Trim = trim;
		Current = default;
	}

	public bool MoveNext ()
	{
		while (true) {
			if (_S.Length == 0) {
				return false;
			}
			char c = _S [0];
			if (Trim && char.IsWhiteSpace (c)) {
				_S = _S.Slice (1);
				continue;
			}
			if (RemoveEmpty && c == Separator) {
				_S = _S.Slice (1);
				continue;
			}
			break;
		}

		var index = _S.IndexOf (Separator);
		if (index > -1) {
			Current = _S.Slice (0, index);
			_S = _S.Slice (index + 1);
		}
		else {
			Current = _S;
			_S = ReadOnlySpan<char>.Empty;
		}

		if (Trim) {
			while (char.IsWhiteSpace (Current [^1])) {
				Current = Current.Slice (0, Current.Length - 1);
			}
		}

		return true;
	}

	public ReadOnlySpan<char> NextOrFail ()
	{
		if (MoveNext ()) {
			return Current;
		}
		throw new NoMoreElementsException (
			"Could not read next element from SplittingEnumerator.",
			new string (_Original));
	}
}

public static class SplittingEnumeratorStringExtensions
{
	public static SplittingEnumerator SpanSplit (this ReadOnlySpan<char> s, char separator, bool removeEmpty, bool trim)
	{
		return new SplittingEnumerator (s, separator, removeEmpty, trim);
	}
}
