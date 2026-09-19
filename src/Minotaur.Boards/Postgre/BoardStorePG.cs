using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using static Minotaur.Utils.Logging;

namespace Minotaur.Boards.Postgre;

public class BoardStorePG : PostgresqlStoreBase<PostgreCtxBoard>
{
	public BoardStorePG (bool limitConnections, bool printSql = false)
		: base (limitConnections, printSql)
	{ }

	private void FixDB (Expression<Func<DbBoard, bool>> where, Action<DbBoard> apply)
	{
		/*
		 * There is a memory leak somewhere. The damn profiler keeps crashing when it has to
		 * handle a couple gigabytes of data, despite being granted a ton of free resources, so
		 * I could not determine what went wrong.
		 * 
		 * Restarting every 2-3 hours circumvents the problem.
		 */

		const int nThreads = 12;
		var sem = new SemaphoreSlim (nThreads);
		long written = 0;
		var start = DateTime.UtcNow;

		void InsertNewRows (List<DbBoard> buffer)
		{
			var swWrite = Stopwatch.StartNew ();
			var swTranslate = Stopwatch.StartNew ();

			using (var c2 = GetContext (true)) {
				c2.UpdateRange (buffer);
				foreach (var b in buffer) {
					apply (b);
				}
				swTranslate.Stop ();

				while (true) {
					try {
						var ret = c2.SaveChanges ();
						Debug.Assert (ret > 0);
						break;
					}
					catch (Exception ex) {
						Console.WriteLine (ex);
					}
				}
			}

			swWrite.Stop ();
			Interlocked.Add (ref written, buffer.Count);
			var dt = (DateTime.UtcNow - start).TotalSeconds;
			LogN (
				$"Wrote {buffer.Count} in {swWrite.Elapsed.TotalSeconds:0.00}s " +
				$"({100.0 * swTranslate.Elapsed.TotalSeconds / swWrite.Elapsed.TotalSeconds:0}% proc) " +
				$"({written:#,##0} in {dt:0.0}s = {1.0 * written / dt:0}/s)");
			sem.Release ();
		}

		void Spawn (List<DbBoard> buf)
		{
			sem.Wait ();
			new Thread (() => InsertNewRows (buf)).Start ();
		}

		using (var c = GetContext ()) {
			LogN ("FixDB: Begin reading rows");
			var src = c.Boards.Where (where);
			var buf = new List<DbBoard> ();
			var t = DateTime.UtcNow;
			foreach (var b in src) {
				buf.Add (b);
				if (buf.Count >= 500 || (DateTime.UtcNow - t).TotalSeconds > 1) {
					Spawn (buf);
					t = DateTime.UtcNow;
					buf = new List<DbBoard> ();
				}
			}
			LogN ("FixDB: Finished reading rows");
			Spawn (buf);
		}
		LogN ("FixDB: Begin waiting for inserter threads");
		for (int i = 0; i < nThreads; i++) {
			sem.Wait ();
		}
		LogN ("FixDB: Finished waiting for inserter threads");
	}

	public DbBoard GetBoard (string hash)
	{
		using var c = GetContext ();
		var b = c.Boards.Find (hash);
		return b;
	}

	public void WithBoards (Action<IQueryable<DbBoard>> a)
	{
		using var c = GetContext ();
		a (c.Boards);
	}

	public T WithBoards<T> (Func<IQueryable<DbBoard>, T> a)
	{
		using var c = GetContext ();
		var res = a (c.Boards);
		return res;
	}

	public IEnumerable<DbBoard> ToBoard (IEnumerable<(string invariantGridName, byte sx, byte sy, byte npieces, int solutionCount, string preview, int solutionSteps)> data)
	{
		return data.Select (d => {
			var b = new DbBoard (d.invariantGridName, d.sx, d.sy, d.npieces, d.solutionCount, d.preview, d.solutionSteps);
			return b;
		});
	}

	public int AddBoards (IEnumerable<DbBoard> data)
	{
		/*
		 * Ensure no duplicates inside the added rows - this can happen with randomized board generation
		 */
		var bs = data
			.GroupBy (b => b.InvariantGridName)
			.Select (g => g.First ())
			.ToArray ();

		int added = 0;
		// Size of chunks: ~1000-ish is faster than 100-ish and 10000-ish
		while (bs.Length > 1500) {
			var chunk = bs [..1000];
			bs = bs [1000..];
			added += AddBoardsImpl (chunk);
		}
		added += AddBoardsImpl (bs);
		return added;
	}

	private int AddBoardsImpl (DbBoard [] chunk)
	{
		while (true) {
			try {
				using var c = GetContext (true);
				int added = c.Boards.UpsertRange (chunk).NoUpdate ().Run ();
				c.SaveChanges ();
				return added;
			}
			catch (TimeoutException ex) {
				LogN (ConsoleColor.Black, Background, ConsoleColor.Red, ex);
				if (ex.InnerException != null) {
					LogN (ex.InnerException);
				}
			}
		}
	}
}
