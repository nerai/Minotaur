using System;

namespace Minotaur.Utils;

public class ResourceAllocator
{
	private readonly MultiwaitSemaphore _Sem;

	public readonly string Name;

	public ulong CurrentlyAvailable => _Sem.CurrentlyAvailable;

	public bool AllowOverdraw { get; set; } = false;

	public ResourceAllocator (
		string name,
		ulong initialCount,
		MultiwaitSemaphore.PrintOptionFlags printOptions = 0)
	{
		Name = name;
		_Sem = new (initialCount);
		_Sem.PrintOptions = printOptions;
	}

	private class Renter : IDisposable
	{
		public readonly MultiwaitSemaphore _Sem;
		public readonly ulong _N;
		private readonly string _Purpose;

		public Renter (MultiwaitSemaphore sem, ulong ticketCount, string purpose)
		{
			_Sem = sem;
			_N = ticketCount;
			_Purpose = purpose;

			_Sem.Acquire (_N, _Purpose);
		}

		public void Dispose ()
		{
			_Sem.Release (_N, _Purpose);
		}
	}

	public IDisposable Rent (ulong ticketCount, string purpose)
	{
		if (ticketCount > _Sem.Maximum) {
			if (AllowOverdraw) {
				ticketCount = _Sem.Maximum;
			}
			else {
				throw new ArgumentOutOfRangeException (nameof (ticketCount));
			}
		}
		return new Renter (_Sem, ticketCount, purpose);
	}

	public void PrintReport ()
	{
		_Sem.PrintReport ();
	}
}
