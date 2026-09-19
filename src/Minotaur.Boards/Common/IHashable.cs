using System.Collections.Immutable;


namespace Minotaur.Boards.Common;

public interface IHashable
{
	// TODO bei matrix: hash braucht nicht board sein, es reicht eine liste der aktiven columns (eher: rows)
	ImmutableArray<byte> GetUniqueHash ();
}
