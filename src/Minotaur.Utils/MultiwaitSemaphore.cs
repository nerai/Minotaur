using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using static Minotaur.Utils.Logging;

namespace Minotaur.Utils;

/// <summary>
/// C# does not offer a multi-wait semaphore for some reason, especially not a fair one.
/// So this is my attempt at building one.
/// It seemed to work properly, with reasonable performance.
/// </summary>
public class MultiwaitSemaphore
{
	[Flags]
	public enum PrintOptionFlags
	{
		None = 0,
		acquire = 1 << 0,
		pass = 1 << 1,
		wait = 1 << 2,
		continu = 1 << 3,
		release = 1 << 4,
		allow = 1 << 5,
	}

	public static readonly PrintOptionFlags PrintOptions_Normal =
		PrintOptionFlags.pass |
		PrintOptionFlags.wait |
		PrintOptionFlags.continu |
		PrintOptionFlags.release |
		0;

	public static readonly PrintOptionFlags PrintOptions_All =
		PrintOptions_Normal |
		PrintOptionFlags.acquire |
		PrintOptionFlags.allow |
		0;

	public PrintOptionFlags PrintOptions;

	[DebuggerDisplay ("{ToString()}")]
	private class QueueEntry
	{
		public readonly ulong TicketCount;
		public readonly ManualResetEventSlim MRE;
		public readonly DateTime Created;
		public readonly string Purpose;

		public double AgeSeconds => (DateTime.UtcNow - Created).TotalSeconds;

		public QueueEntry (ulong ticketCount, ManualResetEventSlim mre, string purpose)
		{
			TicketCount = ticketCount;
			MRE = mre;
			Created = DateTime.UtcNow;
			Purpose = purpose;
		}

		public override string ToString ()
		{
			return $"{Purpose}, {TicketCount} tickets, age {AgeSeconds:0.000}s";
		}
	}

	public readonly ulong Maximum;
	private ulong _Counter;
	private readonly HashSet<QueueEntry> _Waiting = new ();

	public ulong CurrentlyAvailable => Interlocked.Read (ref _Counter);
	public ulong CurrentlyGranted => Maximum - CurrentlyAvailable;

	/// <summary>
	/// Create semaphore with the specified maximum and initial ticket count
	/// </summary>
	public MultiwaitSemaphore (ulong maximum)
	{
		Maximum = maximum;
		_Counter = maximum;
	}

	[MethodImpl (MethodImplOptions.AggressiveInlining)]
	private void PrintEvent (
		PrintOptionFlags requiredFlag,
		ConsoleColor color,
		string eventName,
		ulong ticketCount,
		string purpose,
		double? duration = null)
	{
		if (!PrintOptions.HasFlag (requiredFlag)) {
			return;
		}

		var me = ToString_Synced ();
		using var _ = StartLogBlock;
		LogN (color,
			$"{eventName,8}",
			ResetColor,
			$" {ticketCount,15:#,##0}",
			$" for {purpose,20}");

		string dur;
		if (duration < 10) {
			dur = $"{duration:0.000}s";
		}
		else if (duration < 60) {
			dur = $"{duration:0.00}s";
		}
		else if (duration < 10 * 60) {
			dur = $"{duration / 60:0.0}min";
		}
		else if (duration < 60 * 60) {
			dur = $"{duration / 60:0} min";
		}
		else {
			dur = $"{duration / 3600,3:0.0}hrs";
		}
		Log (duration.HasValue
			? $" after {dur}"
			: $"       " + "      ");

		Log ($"; {me}");
	}

	public void Release (ulong ticketCount, string purpose)
	{
		lock (_Waiting) {
			_Counter += ticketCount;
			if (_Counter > Maximum) {
				// This is not technically necessary, but it helps with the display
				throw new ArgumentOutOfRangeException (nameof (ticketCount));
			}
			PrintEvent (PrintOptionFlags.release, ConsoleColor.Gray, "release", ticketCount, purpose);

			while (_Waiting.Count > 0) {
				// TODO replace this with binary search on a tree for O(log n) search, insert and delete
				var item = _Waiting
					.Where (it => it.TicketCount <= _Counter)
					.MaxBy (it => (it.TicketCount, it.AgeSeconds));
				if (item == null) {
					break;
				}

				_Waiting.Remove (item);
				_Counter -= item.TicketCount;
				PrintEvent (PrintOptionFlags.allow, ConsoleColor.Green, "allow", item.TicketCount, item.Purpose, item.AgeSeconds);

				item.MRE.Set ();
			}
		}
	}

	public void Acquire (ulong ticketCount, string purpose)
	{
		PrintEvent (PrintOptionFlags.acquire, ConsoleColor.Gray, "acquire", ticketCount, purpose);
		QueueEntry qe;
		lock (_Waiting) {
			if (ticketCount <= _Counter && _Waiting.Count == 0) {
				_Counter -= ticketCount;
				PrintEvent (PrintOptionFlags.pass, ConsoleColor.Green, "pass", ticketCount, purpose);
				return;
			}
			qe = new QueueEntry (ticketCount, new ManualResetEventSlim (), purpose);
			_Waiting.Add (qe);
			PrintEvent (PrintOptionFlags.wait, ConsoleColor.Red, "await", ticketCount, purpose);
		}
		qe.MRE.Wait ();
		PrintEvent (PrintOptionFlags.continu, ConsoleColor.Green, "continue", ticketCount, purpose, qe.AgeSeconds);
	}

	public void PrintReport ()
	{
		var me = ToString_Synced ();
		lock (Console.Out) {
			Console.WriteLine (me);
		}
	}

	public string ToString_Synced ()
	{
		string s;
		lock (_Waiting) {
			s = $"Semaphore has {CurrentlyAvailable,15:#,##0}/{Maximum:#,##0}" +
				$", {_Waiting.Count} await {_Waiting.Sum (it => (long) it.TicketCount),15:#,##0}" +
				$" ({Thread.CurrentThread.Name})";
		}
		return s;
	}
}
