using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;

namespace Minotaur.Utils;

/// <summary>
/// Set specialized for small amounts of data.
/// 
/// This does NOT use hashes.
/// </summary>
[DataContract]
public sealed class SmallSet<T> : IEnumerable<T>, IReadOnlyCollection<T>, IDisposable
	where T : IEquatable<T>
{
	const bool UsePool = true;
	private static readonly ArrayPool<T> _Pool = ArrayPool<T>.Shared;

	[DataMember]
	private T [] _A;

	[DataMember]
	private int _Count;

	[IgnoreDataMember]
	public int Count => _Count;

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public SmallSet (int initialCapacity)
	{
		if (UsePool) {
			_A = _Pool.Rent (initialCapacity);
		}
		else {
			_A = new T [initialCapacity];
		}
		_Count = 0;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public SmallSet (IReadOnlyCollection<T> clone)
	{
		if (clone is SmallSet<T> set) {
			_Count = set._Count;
			if (UsePool) {
				_A = _Pool.Rent (_Count);
			}
			else {
				_A = new T [_Count];
			}
			Array.Copy (set._A, _A, _Count);
		}
		else {
			_Count = clone.Count;
			if (UsePool) {
				_A = _Pool.Rent (_Count);
				int i = 0;
				foreach (var it in clone) {
					_A [i] = it;
					++i;
				}
			}
			else {
				_A = clone.ToArray ();
			}
		}
	}

	public override string ToString ()
	{
		if (_Count == 0) {
			return $"{{N = 0;}}";
		}
		if (_Count <= 5) {
			return $"{{N = {_Count}; {string.Join (", ", this)}}}";
		}
		return $"{{N = {_Count}; {string.Join (", ", this.Take (5))}, ...}}";
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public static SmallSet<T> Merge (params SmallSet<T> [] sets)
	{
		if (sets.Length == 0) {
			return new SmallSet<T> (0);
		}

		int n = sets.Sum (set => set.Count);
		var result = new SmallSet<T> (n);
		foreach (var set in sets) {
			result.AddRange (set);
		}
		return result;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public bool Add (T add)
	{
		if (Contains (add)) {
			return false;
		}

		_Count++;
		if (_Count > _A.Length) {
			var newSize = _Count < 4 ? 4 : 2 * _Count;
			if (UsePool) {
				var old = _A;
				_A = _Pool.Rent (newSize);
				Array.Copy (old, _A, old.Length);
				_Pool.Return (old);
			}
			else {
				Array.Resize (ref _A, newSize);
			}
		}
		_A [_Count - 1] = add;
		return true;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public void AddRange (SmallSet<T> set)
	{
		for (int i = set._Count - 1; i >= 0; i--) {
			Add (set._A [i]);
		}
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public bool Contains (T item)
	{
		for (int i = _Count - 1; i >= 0; i--) {
			if (item.Equals (_A [i])) {
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Remove item by moving the list's last item over it
	/// </summary>
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public bool Remove (T item)
	{
		for (int i = _Count - 1; i >= 0; i--) {
			if (item.Equals (_A [i])) {
				_Count--;
				_A [i] = _A [_Count];
				_A [_Count] = default;
				return true;
			}
		}

		return false;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public int RemoveWhere (Predicate<T> match)
	{
		int nRemoved = 0;
		for (int i = _Count - 1; i >= 0; i--) {
			if (match (_A [i])) {
				_Count--;
				_A [i] = _A [_Count];
				_A [_Count] = default;
				nRemoved++;
			}
		}
		return nRemoved;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public T Pull ()
	{
		if (_Count == 0) {
			throw new InvalidOperationException ();
		}

		_Count--;
		var ret = _A [_Count];
		_A [_Count] = default;
		return ret;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public void Clear ()
	{
		Array.Clear (_A, 0, _Count);
		_Count = 0;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public void Sort ()
	{
		Array.Sort (_A, 0, _Count);
	}

	[Obsolete ("Use GetMutableEnumerator instead")]
	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public void Replace (Func<T, T> replace)
	{
		for (int i = _Count - 1; i >= 0; i--) {
			_A [i] = replace (_A [i]);

			// Check that the new item is not a dupe
			for (int check = _Count - 1; check > i; check--) {
				if (_A [i].Equals (_A [check])) {
					_Count--;
					_A [i] = _A [_Count];
					_A [_Count] = default;
					break;
				}
			}
		}
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public void ReplaceMany (Func<T, IEnumerable<T>> replace)
	{
		/*
		 * Read from end to beginning.
		 * Remove each element immediately by replacing it with the last element
		 * Then add all replacements as usual.
		 */
		for (int i = _Count - 1; i >= 0; i--) {
			var old = _A [i];
			_Count--;
			_A [i] = _A [_Count];
			_A [_Count] = default;

			foreach (var add in replace (old)) {
				Add (add);
			}
		}
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public int DiscardDuplicates ()
	{
		int removed = 0;

		for (int i1 = 0; i1 < _Count; ++i1) {
			var it1 = _A [i1];

			for (int i2 = i1 + 1; i2 < _Count; ++i2) {
				var it2 = _A [i2];
				if (!it1.Equals (it2)) {
					// No dupe
					continue;
				}

				/*
				 * It is a dupe.
				 * Because it1 equals it2, we can just remove it2 by replacing it with the tail.
				 * This will remove all copies of the element in a single pass.
				 */
				_Count--;
				_A [i2] = _A [_Count];
				_A [_Count] = default;
				// Check this replaced element again
				--i2;
				++removed;
			}
		}

		return removed;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public IEnumerator<T> GetEnumerator ()
	{
		return new SmallSetEnumerator (this);
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	IEnumerator IEnumerable.GetEnumerator ()
	{
		return GetEnumerator ();
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	public ImmutableArray<T> AsImmutable ()
	{
		return ImmutableArray.Create (_A, 0, _Count);
	}

	/// <summary>
	/// An enumerator that iterates over this set in order from first to last.
	/// It is allowed to add items to this set during the enumeration.
	/// Newly added items will be enumerated over until the end of all items is reached.
	/// 
	/// This is only well-defined for ADDING items. Removing them is NOT allowed during enumeration.
	/// </summary>
	public SmallSetForwardMutableEnumerator GetMutableEnumerator ()
	{
		return new SmallSetForwardMutableEnumerator (this);
	}

	public void Dispose ()
	{
		if (UsePool) {
			_Pool.Return (_A);
		}
	}

	/// <summary>
	/// Iterates backwards.
	/// Elements newly added to the end of the list while iterating are allowed but will NOT be iterated over.
	/// </summary>
	private struct SmallSetEnumerator : IEnumerator<T>
	{
		private readonly SmallSet<T> _Set;
		private int _Index;
		private T _Current;

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public SmallSetEnumerator (SmallSet<T> set)
		{
			_Set = set;
			_Index = set._Count;
			_Current = default (T);
		}

		public T Current => _Current;

		object IEnumerator.Current => _Current;

		public void Dispose ()
		{
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public bool MoveNext ()
		{
			var local = _Set;

			if (_Index > 0) {
				_Index--;
				_Current = local._A [_Index];
				return true;
			}

			_Current = default (T);
			return false;
		}

		void System.Collections.IEnumerator.Reset ()
		{
			_Index = _Set._Count;
			_Current = default (T);
		}
	}

	/// <summary>
	/// Allows adding elements to the end of the list while enumerating over it.
	/// Also allows changing an element in place, as Current is a reference.
	/// 
	/// This does not explicitly implement IEnumerable, but that's not a problem for the compiler.
	/// 
	/// This is a struct and does not alloc on heap.
	/// </summary>
	public struct SmallSetForwardMutableEnumerator
	{
		private readonly SmallSet<T> _Set;
		private int _Index;

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public SmallSetForwardMutableEnumerator (SmallSet<T> set)
		{
			_Set = set;
			_Index = -1;
		}

		public ref T Current {
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			get {
				if (_Index < 0) {
					throw new InvalidOperationException ("Cannot get Current without first MoveNext.");
				}
				var local = _Set;
				return ref local._A [_Index];
			}
		}

		public void Dispose ()
		{
			_Index = int.MinValue;
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public bool MoveNext ()
		{
			if (_Index < _Set._Count - 1) {
				_Index++;
				return true;
			}

			return false;
		}
	}
}

// TODO: vllt bei ints immer sortiert halten damit viele ops schneller gehen?
