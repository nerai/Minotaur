using System.Diagnostics;
using System.Globalization;
using Minotaur.Boards.Strings;
using Minotaur.Utils;

namespace Minotaur.Boards.Optimize;

public class SharedData
{
	public static readonly int ParamCount = new WormyTreeCreationMeta ().GetParams ().Count ();

	private readonly string _BaseDir = $@"Hyperparameter test for strings";
	//private readonly string _BaseDir = $@"../Data2025";

	public readonly List<string> Grids;

	public SharedData ()
	{
		var files = Directory
			.GetFiles ($"{_BaseDir}/boards", "*.txt", SearchOption.TopDirectoryOnly)
			.OrderBy (x => x)
			.ToList ();
		Grids = new List<string> ();
		foreach (var file in files) {
			Grids.AddRange (File.ReadAllLines (file));
		}
		Grids = Grids
			.OrderBy (x => x.Length) // The hardest boards are SHORTEST
			.Distinct ()
			.ToList ();
		Logging.LogN ($"Found {Grids.Count} distinct grids for testing hypers");
	}

	public static void StartMemoryPrinterThread ()
	{
		new Thread (() => {
			int i = 0;
			while (true) {
				Thread.Sleep (1000 * (60 + 10 * i++));
				Console.WriteLine ();
				var a = GC.GetGCMemoryInfo ();
				var b = GC.GetTotalMemory (false);

				Console.WriteLine ($"TotalAvailableMemoryBytes {a.TotalAvailableMemoryBytes / 1024.0 / 1024:0} MB");
				Console.WriteLine ($"HighMemoryLoadThresholdBytes {a.HighMemoryLoadThresholdBytes / 1024.0 / 1024:0} MB");

				Console.WriteLine ($"Whole System MemoryLoadBytes {a.MemoryLoadBytes / 1024.0 / 1024:0} MB");

				Console.WriteLine ($"TotalCommittedBytes {a.TotalCommittedBytes / 1024.0 / 1024:0} MB");
				Console.WriteLine ($"HeapSizeBytes {a.HeapSizeBytes / 1024.0 / 1024:0} MB");
				Console.WriteLine ($"GetTotalMemory {b / 1024.0 / 1024:0} MB");

				Console.WriteLine ($"PauseTimePercentage {a.PauseTimePercentage} %");
				Console.WriteLine ($"FinalizationPendingCount {a.FinalizationPendingCount}");
			}
		}).Start ();
	}
}
