
namespace Minotaur.Utils.Dictionaries;

public interface IBulkDictionary<TK, TV> : IDisposable
	where TK : notnull
{
	long Count { get; }

	IEnumerable<TV> Values { get; }

	public delegate TV AddFactory (TK key, TV val);
	public delegate TV UpdateFactory (TK key, TV val, TV existingVal);

	void AddOrUpdate (
		TK key,
		TV val,
		AddFactory addFactory,
		UpdateFactory updateFactory);

	void BulkAddOrUpdate (
		IEnumerable<TV> values,
		Func<TV, TK> keyF,
		AddFactory addFactory,
		UpdateFactory updateFactory);

	bool TryGetValue (TK key, out TV val);
}
