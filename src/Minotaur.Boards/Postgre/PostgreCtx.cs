using System;
using System.Collections.Concurrent;
using System.Threading;

using Microsoft.EntityFrameworkCore;
using Npgsql;

using static Minotaur.Utils.Logging;

namespace Minotaur.Boards.Postgre;

public abstract class PostgreCtx : DbContext
{
	private readonly DateTime _tCreated = DateTime.UtcNow;
	private bool _Disposed = false;

	internal bool PrintSql;
	private SemaphoreSlim _Sem;

	public PostgreCtx ()
	{
	}

	public void SetSemaphore (SemaphoreSlim sem)
	{
		if (_Sem != null) {
			throw new InvalidOperationException ("Semaphore should be set only once.");
		}
		_Sem = sem;
	}

	protected override void OnModelCreating (ModelBuilder modelBuilder)
	{
		base.OnModelCreating (modelBuilder);

		var dt = DateTime.UtcNow - _tCreated;
		if (dt.TotalSeconds > 1) {
			LogN (ConsoleColor.Cyan, $"PostgreCtx model created after {dt.TotalSeconds:0.0}s");
		}
	}

	protected override void OnConfiguring (DbContextOptionsBuilder optionsBuilder)
	{
		if (PrintSql) {
			optionsBuilder.LogTo (Console.WriteLine);
		}

		var cb = new NpgsqlConnectionStringBuilder ();

		// Address
		cb.Host = "localhost";
		cb.Port = 5433;

		// Login
		cb.Database = "Pentomino";
		cb.Username = "Pentomino";
		cb.Password = "asdf";

		// Connection timeout
		cb.Timeout = 60;

		// Disable timeout for slow connections
		cb.CommandTimeout = 0;
		//cb.InternalCommandTimeout = 0;

		// Avoid buffer thrashing for large single values (default is 8k)
		cb.ReadBufferSize = 1 * 1024 * 1024;
		cb.WriteBufferSize = 1 * 1024 * 1024;

		// Other
		cb.ApplicationName = Environment.CommandLine;
		cb.IncludeErrorDetail = true;

		// Create connection
		var cons = cb.ToString ();
		optionsBuilder.UseNpgsql (cons);

		var dt = DateTime.UtcNow - _tCreated;
		if (dt.TotalSeconds > 5) {
			LogN (ConsoleColor.Cyan, $"PostgreCtx configured after {dt.TotalSeconds:0.0}s");
		}
	}

	public override void Dispose ()
	{
		if (_Disposed) {
			return;
		}
		_Disposed = true;

		base.Dispose ();
		_Sem?.Release ();

		var dt = DateTime.UtcNow - _tCreated;
		if (dt.TotalSeconds > 60) {
			LogN (ConsoleColor.Cyan, $"PostgreCtx disposed after {dt.TotalSeconds:0}s");
		}
	}
}
