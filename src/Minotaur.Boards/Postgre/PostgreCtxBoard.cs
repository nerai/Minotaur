using System.Threading;

using Microsoft.EntityFrameworkCore;

namespace Minotaur.Boards.Postgre;

public class PostgreCtxBoard : PostgreCtx
{
	public PostgreCtxBoard ()
	{ 
	}

	public DbSet<DbBoard> Boards { get; set; }
}
