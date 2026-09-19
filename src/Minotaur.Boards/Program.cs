using System.Diagnostics;
using System.Globalization;
using Minotaur.Boards.Optimize;
using Minotaur.Boards.Strings;
using Minotaur.Utils;


class Program
{
	static void Main (string [] args)
	{
		if (args.Length == 0) {
			new RunSimplex (null);
		}
		else if (args.Length == 0) {
			new RunSimplex (int.Parse (args.Single ()));
		}
		else {
			new RunSingleConfiguration (args);
		}
		//Console.WriteLine ("END");
	}
}
