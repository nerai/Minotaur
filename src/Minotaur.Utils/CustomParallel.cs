using System;
using System.Collections.Generic;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;

using static Minotaur.Utils.Logging;

namespace Minotaur.Utils;

// This entire class is an exercise in running programs resp. parts of a program in parallel that have no business being run in parallel.
// It did work, but it was not pretty.
public static class CustomParallel
{
	private static readonly ConcurrentDictionary<string, uint> _GlobalLimits = new ();
	private static readonly CustomThreadPool Pool = new ();

	/// <summary>
	/// Perform an operation in parallel, similar to System.Threading.Tasks.Parallel.
	/// </summary>
	/// <typeparam name="T">
	/// Type of the enumerable
	/// </typeparam>
	/// <param name="name">
	/// Individual name for this parallel operation. Can be customized.
	/// </param>
	/// <param name="maxParallelLocal">
	/// Max parallelism for this operation using the specified enumerator.
	/// </param>
	/// <param name="resourceName">
	/// An identifying name shared between similar operations.
	/// This is used to group similar operations.
	/// 
	/// This can be useful even if the resource itself implements a limit beyond which it
	/// blocks, because it prevents the reation of additional work threads which would just
	/// block on the resource.
	/// </param>
	/// <param name="maxParallelGlobal">
	/// Max parallelism for all similar operations (sharing a resourceName).
	/// This is used to limit the number of operations globally, regardless of the local limit.
	/// </param>
	/// <param name="additionalWorkCache">
	/// Additional cache size for work.
	/// Use this if each work package is very small.
	/// This is usually not needed, there will be some work cached for all threads anyway.
	/// </param>
	/// <param name="priority">
	/// Thread priority.
	/// </param>
	/// <param name="expectedTotalLifetimeSeconds">
	/// If the expected lifetime is vastly exceeded, a message will be printed.
	/// </param>
	/// <param name="enumerable">
	/// Enumerable to work on.
	/// It will be enumerated exactly once.
	/// </param>
	/// <param name="action">
	/// Action to take on each enumerated value.
	/// </param>
	public static void ForEach<T> (
		string name,
		int maxParallelLocal,
		string? resourceName, // todo this should be used everywhere
		uint? maxParallelGlobal,
		int additionalWorkCache,
		ThreadPriority priority,
		double expectedTotalLifetimeSeconds,
		IEnumerable<T> enumerable,
		Action<T> action)
	{
		using var to = new TimedOperation (name, 1 + 10 * expectedTotalLifetimeSeconds);
		using var q = new BlockingCollection<T> (1 + 2 * maxParallelLocal + additionalWorkCache);
		var waitingOn = new SemaphoreSlim (0);
		var usedThreads = 0;

		/*
		 * Make the global resource known.
		 */
		if (resourceName != null) {
			_GlobalLimits.TryAdd (resourceName, 0u);
		}

		/*
		 * This is the work loop.
		 * It blocks if the work queue is empty.
		 */
		void work ()
		{
			foreach (var item in q.GetConsumingEnumerable ()) {
				action (item);
			}

			if (resourceName != null) {
				_GlobalLimits.AddOrUpdate (
					resourceName,
					key => throw new InvalidProgramException (),
					(key, existing) => checked(existing - 1u));
			}

			waitingOn.Release ();
		}

		/*
		 * This tries to get a global ticket, if they apply.
		 * This does not block, and the limit can be overridden.
		 */
		bool TryAcquireGlobalTicket (bool force)
		{
			/* TODO
			for (int i = 0; i < usedThreads; i++) {
				if (ts [i].ThreadState != System.Threading.ThreadState.Running) {
					/*
					 * There is already a worker, but it is not (efficiently and effectively) working.
					 * This may be because it is transitioning into another state, suspended or blocked.
					 * It would probably be useless to create another.
					 * 
					 * Note: Threads waiting for I/O are still considered as Running, so they are exempted.
					 */
			/*
					return false;
				}
			}
			*/

			if (resourceName == null) {
				return true;
			}

			bool ok = false;
			_GlobalLimits.AddOrUpdate (
				resourceName,
				key => throw new InvalidProgramException (),
				(key, existing) => {
					if (!force) {
						if (existing >= maxParallelGlobal) {
							ok = false;
							return existing;
						}
					}
					ok = true;
					return checked(existing + 1u);
				});
			return ok;
		}

		/*
		 * This starts a new work loop.
		 * If global tickets apply, one is acquired if possible.
		 * This does not block, without a ticket it will just do nothing.
		 */
		void StartNewThread (bool force)
		{
			Debug.Assert (usedThreads < maxParallelLocal);

			var ok = TryAcquireGlobalTicket (force);
			if (!ok) {
				Debug.Assert (!force);
				return;
			}

			if (usedThreads == 0) {
				//LogN ($"{name} Spawn first worker");
			}
			else {
				//LogN ($"{name} Spawn worker #{usedThreads + 1}");
			}
			++usedThreads;
			Debug.Assert (usedThreads > 0);
			Debug.Assert (usedThreads <= maxParallelLocal);

			var sut = maxParallelLocal >= 10 ? $"{usedThreads:00}" : $"{usedThreads}";
			var tname = $"{name} {sut}/{maxParallelLocal}";
			Pool.AddTask (tname, priority, work);
		}

		/*
		 * One work loop is always created, regardless of global tickets.
		 */
		StartNewThread (true);
		Debug.Assert (usedThreads > 0);
		Debug.Assert (waitingOn.CurrentCount == 0);

		/*
		 * Fill the work queue, and if useful create more work loops.
		 */
		var qMaxSize = q.BoundedCapacity;
		var workCount = 0ul;
		foreach (var item in enumerable) {
			q.Add (item);
			++workCount;
			if (usedThreads >= maxParallelLocal) {
				continue;
			}
			var c = q.Count;
			if (c <= 1) {
				continue;
			}
			var desiredThreadCount = 1 + maxParallelLocal * c / (qMaxSize + 1);
			if (usedThreads >= desiredThreadCount) {
				continue;
			}
			//LogN ($"{name} Try to start additional worker");
			StartNewThread (false);
		}
		Debug.Assert (usedThreads > 0);
		Debug.Assert (usedThreads <= maxParallelLocal);

		/*
		 * Work done, join.
		 */
		if (usedThreads > 1) {
			//LogN ($"{name} Completed adding, join {usedThreads} workers");
		}
		q.CompleteAdding ();
		for (int i = 0; i < usedThreads; i++) {
			waitingOn.Wait ();
		}

		if (to.SW.Elapsed.TotalSeconds >= 60) {
			LogN ($"{name} used {usedThreads}/{maxParallelLocal} threads for {workCount} tasks in {to.SW.Elapsed.TotalSeconds:0.0}s.");
		}
	}
}
