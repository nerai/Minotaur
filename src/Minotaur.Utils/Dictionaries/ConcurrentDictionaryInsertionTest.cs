using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Minotaur.Utils;

namespace Minotaur.Utils.Dictionaries;

public class ConcurrentDictionaryInsertionTest
{
	public ConcurrentDictionaryInsertionTest ()
	{
		SortedDictionary<uint, uint> d_SortList = null;
		Dictionary<uint, uint> d_LockA = null;
		Dictionary<uint, uint> d_LockI = null;
		ConcurrentDictionary<uint, uint> d_Conc = null;
		Dictionary<uint, uint> d_LockSplit0 = null;
		Dictionary<uint, uint> d_LockSplit1 = null;
		ParallelDictionary<uint, uint> d_Par = null;
		ParallelDictionary<uint, uint> d_ParBulk = null;
		ParallelDictionary<uint, uint> d_ParBulkThr = null;

		void startThreads (int run, (Action<uint> worker, Action end) tup)
		{
			MemoryPressure.Collect (false);

			var ts = new List<Thread> ();
			for (uint i = 0; i < 24; i++) {
				var ii = i;
				var t = new Thread (() => {
					tup.worker (ii);
				});
				ts.Add (t);
			}
			var sw = Stopwatch.StartNew ();
			ts.ForEach (t => t.Start ());
			ts.ForEach (t => t.Join ());
			tup.end?.Invoke ();
			Console.WriteLine ($"{sw.Elapsed.TotalSeconds:0.000} {tup.worker.Method.Name}");
		}

		const int N = 100_000_000;

		void workSortA (uint index)
		{
			lock (d_SortList) {
				for (uint i = index; i < N; i += 24) {
					if (d_SortList.TryGetValue (i, out var existing)) {
						d_SortList [i] = existing + i;
					}
					else {
						d_SortList [i] = i;
					}
				}
			}
		}

		void workLockA (uint index)
		{
			lock (d_LockA) {
				for (uint i = index; i < N; i += 24) {
					if (d_LockA.TryGetValue (i, out var existing)) {
						d_LockA [i] = existing + i;
					}
					else {
						d_LockA [i] = i;
					}
				}
			}
		}

		void workLockI (uint index)
		{
			for (uint i = index; i < N; i += 24) {
				lock (d_LockI) {
					if (d_LockI.TryGetValue (i, out var existing)) {
						d_LockI [i] = existing + i;
					}
					else {
						d_LockI [i] = i;
					}
				}
			}
		}

		void workConc (uint index)
		{
			for (uint i = index; i < N; i += 24) {
				d_Conc.AddOrUpdate (i, i, (key, existing) => {
					return existing + i;
				});
			}
		}

		void workSplit (uint index)
		{
			var temp0 = new List<uint> ();
			var temp1 = new List<uint> ();

			for (uint i = index; i < N; i += 24) {
				var into = i % 2 == 0
					? temp0
					: temp1;
				into.Add (i);
			}

			void insert (Dictionary<uint, uint> to, List<uint> add)
			{
				lock (to) {
					foreach (var i in add) {
						if (to.TryGetValue (i, out var existing)) {
							to [i] = existing + i;
						}
						else {
							to [i] = i;
						}
					}
				}
			}
			insert (d_LockSplit0, temp0);
			insert (d_LockSplit1, temp1);
		}

		void workSplitEnd ()
		{
			var merge = new Dictionary<uint, uint> (d_LockSplit0.Count + d_LockSplit1.Count);
			foreach (var pair in d_LockSplit0) {
				merge [pair.Key] = pair.Value;
			}
			foreach (var pair in d_LockSplit1) {
				merge [pair.Key] = pair.Value;
			}
		}

		void workPar (uint index)
		{
			for (uint i = index; i < N; i += 24) {
				d_Par.AddOrUpdate (i, i, (k, v) => v, (key, v, existing) => {
					return existing + v;
				});
			}
		}

		void workParBulk (uint index)
		{
			IEnumerable<(uint, uint)> generate ()
			{
				for (uint i = index; i < N; i += 24) {
					yield return (i, i);
				}
			}

			uint addF (uint key, uint val)
			{
				return val;
			}

			uint updateF (uint key, uint val, uint existing)
			{
				return val + existing;
			}

			d_ParBulk.BulkAddOrUpdate (
				generate (),
				pair => pair.Item1,
				pair => pair.Item2,
				addF,
				updateF);
		}

		void workParBulkThr (uint index)
		{
			IEnumerable<(uint, uint)> generate ()
			{
				for (uint i = index; i < N; i += 24) {
					yield return (i, i);
				}
			}

			uint addF (uint key, uint val)
			{
				return val;
			}

			uint updateF (uint key, uint val, uint existing)
			{
				return val + existing;
			}

			d_ParBulkThr.BulkAddOrUpdateThreaded (generate (), addF, updateF);
		}

		for (int i = 0; i < 5; i++) {
			Console.WriteLine ($"Run {i}");
			d_SortList = new ();
			d_LockA = new (N);
			d_LockI = new (N);
			d_Conc = new (24, N);
			d_LockSplit0 = new (N / 2 + 1);
			d_LockSplit1 = new (N / 2 + 1);
			d_Par = new ();
			d_ParBulk = new ();
			d_ParBulkThr = new ();

			var works = new (Action<uint> worker, Action? end) [] {
				//workConc,
				//workSortA,
				//(workLockI, null),
				//(workPar, null),
				
				//(workSplit, workSplitEnd),
				//(workLockA, null),
				(workParBulk, null),
				//(workParBulkThr, null),
			};
			works.Shuffle ();
			for (int j = 0; j < works.Length; j++) {
				startThreads (i, works [(i + j) % works.Length]);
			}
		}
	}
}
