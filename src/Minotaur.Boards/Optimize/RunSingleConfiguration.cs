using System.Diagnostics;
using System.Xml.Linq;
using Minotaur.Utils;

namespace Minotaur.Boards.Optimize;

class RunSingleConfiguration
{
	private readonly SharedData _Data;
	private readonly SharedResults _Results;

	public RunSingleConfiguration (string [] args)
	{
		Logging.LogN ("Hyperparameter test");
		Process.GetCurrentProcess ().PriorityClass = ProcessPriorityClass.Idle;

		_Data = new ();
		_Results = new SharedResults ();

		Logging.LogN ($"There are {args.Length} args.");
		//Logging.LogN (string.Join (" ", args));

		if (args.Length != SharedData.ParamCount) {
			throw new InvalidOperationException ();
		}

		var param = args.Select (a => double.Parse (a)).ToArray ();
		var r = new ConfigurationRunner (_Data, _Results);
		var loss = r.RunConfig (param);

		Thread.Sleep (1000);
		var line = string.Join (" ", loss.Select (x => x.UCost));
		Console.WriteLine ($"\n---RESULT---\n{line}");
		Environment.Exit (0);
	}
}
