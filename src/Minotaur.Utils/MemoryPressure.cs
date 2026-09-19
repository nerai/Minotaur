using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Minotaur.Utils;

public static class MemoryPressure
{
	public static void Collect (bool verbose)
	{
		if (verbose) {
			PrintGcStats ("Before collect: ");
		}
		GC.Collect (GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
		GC.WaitForPendingFinalizers ();
		Thread.Sleep (100);
		if (verbose) {
			PrintGcStats ("After collect:  ");
		}
	}

	public static void PrintGcStats (string prefix)
	{
		Console.Write (prefix);

		long totalMemory = GC.GetTotalMemory (forceFullCollection: false);
		Console.Write ($"Total: {totalMemory / 1024.0 / 1024 / 1024:0.0} GB. ");

		/*
		Console.Write ($"Gen collections: ");
		for (int gen = 0; gen <= GC.MaxGeneration; gen++) {
			Console.Write ($"{GC.CollectionCount (gen)}, ");
		}
		Console.WriteLine ();
		*/

		GCMemoryInfo info = GC.GetGCMemoryInfo ();
		//Console.Write ($"Heap {info.HeapSizeBytes / 1024.0 / 1024 / 1024:0.0} GB. ");
		//Console.Write ($"Fragmented {info.FragmentedBytes / 1024.0 / 1024:0.0} MB, ");
		Console.Write ($"{info.MemoryLoadBytes / 1024.0 / 1024 / 1024:0.0}/{info.TotalAvailableMemoryBytes / 1024.0 / 1024 / 1024:0.0} GB used");
		Console.WriteLine ();
	}
}
