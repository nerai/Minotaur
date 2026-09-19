using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;

namespace Minotaur.Utils.Dictionaries;

public class ParallelDictionary<TK, TV> : IBulkDictionary<TK, TV>, IDisposable
	where TK : notnull
{
	private readonly Dictionary<TK, TV> [] _D;

	// Should be power of two and probably a little more than thread count. Depends on good hash function.
	private const uint Parallelism = 64;

	private long _Count = 0;
	public long Count => _Count;

	const bool UseListPool = true;
	private static readonly ObjectPool<List<List<(TK, TV)>>> _ListPool = new DefaultObjectPoolProvider () {
		MaximumRetained = Environment.ProcessorCount * 2
	}.Create<List<List<(TK, TV)>>> ();

	const bool UseDictPool = true;
	private static readonly ObjectPool<Dictionary<TK, TV>> _DictPool = new DefaultObjectPoolProvider () {
		MaximumRetained = Environment.ProcessorCount * (int) Parallelism * 2
	}.Create<Dictionary<TK, TV>> ();

	public ParallelDictionary ()
	{
		if (!BitOperations.IsPow2 (Parallelism)) {
			throw new ArgumentException ("Should be power of two", nameof (Parallelism));
		}

		_D = new Dictionary<TK, TV> [Parallelism];
		for (int i = 0; i < Parallelism; i++) {
			if (UseDictPool) {
				_D [i] = _DictPool.Get ();
			}
			else {
				_D [i] = new ();
			}
		}
	}

	public override string ToString ()
	{
		return $"Count: {_Count}, par: {Parallelism}";
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	private uint GetBucket (int hashCode)
	{
		return unchecked((uint) hashCode) & (Parallelism - 1u);
	}

	public void AddOrUpdate (
		TK key,
		TV val,
		IBulkDictionary<TK, TV>.AddFactory addFactory,
		IBulkDictionary<TK, TV>.UpdateFactory updateFactory
		)
	{
		var into = GetBucket (key.GetHashCode ());
		var d = _D [into];
		lock (d) {
			if (d.TryGetValue (key, out var existing)) {
				val = updateFactory (key, val, existing);
			}
			else {
				val = addFactory (key, val);
				Interlocked.Increment (ref _Count);
			}
			d [key] = val;
		}
	}

	public void BulkAddOrUpdate<T> (
		IEnumerable<T> items,
		Func<T, TK> keyF,
		Func<T, TV> valF,
		IBulkDictionary<TK, TV>.AddFactory addFactory,
		IBulkDictionary<TK, TV>.UpdateFactory updateFactory
		)
	{
		List<List<(TK, TV)>> sort;
		if (UseListPool) {
			sort = _ListPool.Get ();
			if (sort.Count == 0) {
				// ObjectPool sadly has no factory API, so create it here instead
				sort = null;
			}
		}
		if (sort == null) {
			sort = Enumerable
				.Range (0, (int) Parallelism)
				.Select (i => new List<(TK, TV)> ())
				.ToList ();
		}

		foreach (var item in items) {
			var key = keyF (item);
			var val = valF (item);
			var into = GetBucket (key.GetHashCode ());
			sort [(int) into].Add ((key, val));
		}

		var added = 0;
		//foreach (int i in Enumerable.Range (0, _Parallelism_).Shuffled ()) {
		for (uint i = 0; i < Parallelism; i++) {
			var d = _D [i];
			var list = sort [(int) i];
			if (list.Count > 0) {
				lock (d) {
					foreach (var (key, val) in list) {
						TV insert;
						if (d.TryGetValue (key, out var existing)) {
							insert = updateFactory (key, val, existing);
						}
						else {
							insert = addFactory (key, val);
							++added;
						}
						d [key] = insert;
					}
				}
			}
		}
		if (UseListPool) {
			for (uint i = 0; i < Parallelism; i++) {
				var list = sort [(int) i];
				list.Clear ();
				if (list.Capacity > 10_000) {
					list.TrimExcess ();
				}
			}
			_ListPool.Return (sort);
		}
		Interlocked.Add (ref _Count, added);
	}

	public void BulkAddOrUpdate (
		IEnumerable<TV> values,
		Func<TV, TK> keyF,
		IBulkDictionary<TK, TV>.AddFactory addFactory,
		IBulkDictionary<TK, TV>.UpdateFactory updateFactory
		)
	{
		BulkAddOrUpdate (
			values,
			keyF,
			v => v,
			addFactory,
			updateFactory);
	}

	[Obsolete]
	public void BulkAddOrUpdateThreaded (
		IEnumerable<(TK key, TV val)> pairs,
		IBulkDictionary<TK, TV>.AddFactory addFactory,
		IBulkDictionary<TK, TV>.UpdateFactory updateFactory
		)
	{
		void InsertList (List<(TK key, TV val)> list, uint into)
		{
			var d = _D [into];
			var added = 0;
			lock (d) {
				foreach (var pair in list) {
					var (key, val) = pair;
					if (d.TryGetValue (key, out var existing)) {
						val = updateFactory (key, val, existing);
					}
					else {
						val = addFactory (key, val);
						++added;
					}
					d [key] = val;
				}
			}
			Interlocked.Add (ref _Count, added);
		}

		var sort = new List<(TK key, TV val)> [Parallelism];
		var ts = new List<Thread> ();
		foreach (var pair in pairs) {
			var into = GetBucket (pair.key.GetHashCode ());
			var list = sort [into];
			if (list == null) {
				list = new List<(TK key, TV val)> (capacity: 16350);
				sort [into] = list;
			}

			list.Add (pair);
			if (list.Count >= 16300) {
				sort [into] = null;
				var t = new Thread (() => {
					InsertList (list, into);
				});
				t.Start ();
			}
		}
		for (uint into = 0; into < Parallelism; into++) {
			var list = sort [into];
			if (list == null) {
				continue;
			}
			InsertList (list, into);
		}
		ts.ForEach (t => t.Join ());
	}

	/// <summary>
	/// This is NOT thread safe
	/// </summary>
	public IEnumerable<TV> Values {
		get {
			for (uint i = 0; i < Parallelism; i++) {
				var d = _D [i];
				foreach (var val in d.Values) {
					yield return val;
				}
			}
		}
	}

	public bool TryGetValue (TK key, out TV val)
	{
		var into = GetBucket (key.GetHashCode ());
		var d = _D [into];
		lock (d) {
			return d.TryGetValue (key, out val);
		}
	}

	/// <summary>
	/// Merge another dict into this one.
	/// 
	/// MT safe.
	/// </summary>
	public void Merge (ParallelDictionary<TK, TV> with)
	{
		foreach (var dict in with._D) {
			BulkAddOrUpdate (
				dict,
				pair => pair.Key,
				pair => pair.Value,
				(key, val) => val,
				(key, val, existing) => throw new InvalidDataException ("Duplicates are not allowed in Dictionary merging."));
		}
	}

	public void Dispose ()
	{
		if (UseDictPool) {
			for (uint i = 0; i < Parallelism; i++) {
				var d = _D [i];
				d.Clear ();
				_DictPool.Return (d);
			}
		}
	}
}
