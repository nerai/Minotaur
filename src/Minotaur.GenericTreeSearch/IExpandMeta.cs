#define DEBUG_PRINT


namespace Minotaur.GenericTreeSearch;

public interface IExpandMeta
{
	long ForciblyPruneAfterVisits { get; }
	bool PruneImmediately { get; }
}

public class FixedExpandMeta : IExpandMeta
{
	public long ForciblyPruneAfterVisits => long.MaxValue;
	public bool PruneImmediately => false;
}
