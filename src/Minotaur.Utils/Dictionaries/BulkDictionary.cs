
namespace Minotaur.Utils.Dictionaries;

public class BulkDictionary<TK, TV> : IBulkDictionary<TK, TV>
	where TK : notnull
{
	private readonly Dictionary<TK, TV> _D;

	public long Count => _D.Count;

	public BulkDictionary (
		int initialCapacity = 1_000_000)
	{
		_D = new (initialCapacity);
	}

	public void AddOrUpdate (
		TK key,
		TV val,
		IBulkDictionary<TK, TV>.AddFactory addFactory,
		IBulkDictionary<TK, TV>.UpdateFactory updateFactory
		)
	{
		lock (_D) {
			if (_D.TryGetValue (key, out var existing)) {
				val = updateFactory (key, val, existing);
			}
			else {
				val = addFactory (key, val);
			}
			_D [key] = val;
		}
	}

	public void BulkAddOrUpdate (
		IEnumerable<TV> values,
		Func<TV, TK> keyF,
		IBulkDictionary<TK, TV>.AddFactory addFactory,
		IBulkDictionary<TK, TV>.UpdateFactory updateFactory
		)
	{
		lock (_D) {
			foreach (var val in values) {
				var key = keyF (val);
				TV insert;
				if (_D.TryGetValue (key, out var existing)) {
					insert = updateFactory (key, val, existing);
				}
				else {
					insert = addFactory (key, val);
				}
				_D [key] = insert;
			}
		}
	}

	public IEnumerable<TV> Values => _D.Values;

	public bool TryGetValue (TK key, out TV val)
	{
		lock (_D) {
			return _D.TryGetValue (key, out val);
		}
	}

	public void Dispose () { }
}
