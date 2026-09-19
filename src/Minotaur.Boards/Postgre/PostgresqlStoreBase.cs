using System;
using System.Threading;

using static Minotaur.Utils.Logging;

namespace Minotaur.Boards.Postgre;

public class PostgresqlStoreBase<T> where T : PostgreCtx, new()
{
	private static readonly SemaphoreSlim _Sem = new (10);
	private readonly bool _Limited;
	internal bool PrintSql;

	public PostgresqlStoreBase (bool limitConnections, bool printSql)
	{
		_Limited = limitConnections;
		PrintSql = printSql;
	}

	protected T GetContext (bool overrideLimit = false)
	{
		var t = new T ();
		t.PrintSql = PrintSql;

		if (_Limited && !overrideLimit) {
			if (_Sem.Wait (0)) {
				// immediately ok
			}
			else {
				using (var _ = StartLogBlock) {
					LogF (Background, ConsoleColor.Yellow);
					LogF (ConsoleColor.Black);
					//LogF ($"[{_Sem.CurrentCount}]");
					LogF ($"ß");
				}
				_Sem.Wait ();
			}
			t.SetSemaphore (_Sem);
		}

		return t;
	}
}
