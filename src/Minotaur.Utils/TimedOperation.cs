using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Minotaur.Utils;

public class TimedOperation : IDisposable
{
	public string Name;

	/// <summary>
	/// In seconds.
	/// </summary>
	public double PrintWhenLongerThan;

	public readonly Stopwatch SW;

	public TimedOperation (string name, double printWhenLongerThan = 1.0)
	{
		Name = name;
		PrintWhenLongerThan = printWhenLongerThan;
		SW = Stopwatch.StartNew ();
	}

	public void Dispose ()
	{
		SW.Stop ();
		var secs = SW.Elapsed.TotalSeconds;
		if (secs >= PrintWhenLongerThan) {
			Logging.LogN ($"<{Name}> took {secs:0.000}s");
		}
	}
}
