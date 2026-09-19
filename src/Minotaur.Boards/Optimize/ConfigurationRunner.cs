using System.Diagnostics;
using Minotaur.Boards.Strings;
using Minotaur.Utils;

namespace Minotaur.Boards.Optimize;

class ConfigurationRunner
{
	public bool PrintProgress = 1111 == 1111;
	private readonly SharedData _Data;
	private readonly SharedResults _Res;

	public int _EvaluationsCompletedByCache = 0;
	public int _EvaluationsCompletedThisSession = 0;
	const bool LoadExistingResults = false;

	public ConfigurationRunner (SharedData data, SharedResults res)
	{
		_Data = data;
		_Res = res;
	}

	public double Loss (IEnumerable<ulong> costs, ICollection<double> param)
	{
		var lcost = costs.Select (c => Math.Log2 (c)).ToArray ();
		if (lcost.Length != _Data.Grids.Count) {
			throw new InvalidOperationException ();
		}
		var cost = lcost.Sum ();

		if (param.Count != SharedData.ParamCount) {
			throw new InvalidOperationException ();
		}
		//cost += param.Sum (p => Math.Abs (p) / 300);

		return cost;
	}

	public List<Result> RunConfig (double [] param)
	{
		var remaining = _Data.Grids.Count;
		var popt = new ParallelOptions () {
			MaxDegreeOfParallelism = 12,
		};
		var ress = new List<Result> ();
		void work (string grid)
		{
			var res = WorkSingle (param, grid);
			if (PrintProgress) {
				var rem = Interlocked.Decrement (ref remaining);
				if (rem < 10 || rem % 10 == 0) {
					Logging.LogF ($"<{rem}>");
				}
				lock (ress) {
					ress.Add (res);
				}
			}
		}
		Parallel.ForEach (_Data.Grids.Shuffled (), popt, work);
		return ress;
	}

	public double RunConfigWithLoss (double [] param)
	{
		var ress = RunConfig (param);
		return Loss (ress.Select (r => r.UCost), param);
	}

	private Result WorkSingle (double [] param, string grid)
	{
		if (LoadExistingResults) {
			var copy = _Res.CopyResults ();

			foreach (var it in copy.ToList ()) {
				if (it.Grid != grid) {
					continue;
				}
				var ok = true;
				for (var i = 0; i < param.Length; i++) {
					var d1 = param [i];
					var d2 = it.Param [i];
					if (Math.Abs (d1 - d2) > 0.01) {
						ok = false;
						break;
					}
				}
				if (!ok) {
					continue;
				}

				if (PrintProgress) {
					Logging.LogF (".");
				}
				Interlocked.Increment (ref _EvaluationsCompletedByCache);
				return it;
			}
		}

		var cache = new WormyTreeCreator (false);
		cache.PrintToConsole = false;

		var meta = new WormyTreeCreationMeta ();
		meta.SetParams (param);
		//Logging.LogN ($"C_Base: {meta.Desire_Choice_Base}, D_Base: {meta.Desire_Decision_Base}, C_P1_A: {meta.Desire_Choice_P1_A}, C_P1_S: {meta.Desire_Choice_P1_S}");

		var treeRoot = cache.CreateTree (grid, ulong.MaxValue, meta);
		Debug.Assert (treeRoot.IsSolvable == true);

		var ucost = (treeRoot.LocalCost + treeRoot.MinCostToFinish).AsUlong ();
		var res = new Result (param, grid, ucost);
		_Res.AddResult (res);
		_Res.LogTsv (res);
		Interlocked.Increment (ref _EvaluationsCompletedThisSession);
		return res;
	}
}
