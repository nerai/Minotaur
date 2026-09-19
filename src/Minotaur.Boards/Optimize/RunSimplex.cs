using System.Diagnostics;
using Minotaur.Boards.Strings;
using Minotaur.Utils;
using Accord.Math.Optimization;
using Accord.Statistics.Kernels;
using System.Security.Cryptography;
using System.Collections;
using K4os.Hash.xxHash;
using Accord;
using System.Globalization;
using SharpLearning.Optimization;
using Accord.MachineLearning.Bayes;
using Accord.Genetic;
using Accord.MachineLearning.VectorMachines.Learning;
using Microsoft.EntityFrameworkCore.Storage;
using Accord.MachineLearning.Boosting;
using System.Runtime.Intrinsics.Arm;
using System;
using System.Text;
using Accord.Math.Optimization.Losses;

namespace Minotaur.Boards.Optimize;

class RunSimplex
{
	private readonly int? _RandomSeed;
	private DateTime _T0;
	private double _BestLoss = 999999;
	public bool PrintProgress = false;
	private readonly SharedData _Data;
	private readonly ConfigurationRunner _R;

	private void UpdateTitle ()
	{
		var iters = (_R._EvaluationsCompletedThisSession + _R._EvaluationsCompletedByCache) / _Data.Grids?.Count;
		var dt = (DateTime.UtcNow - _T0).TotalSeconds;
		var frac = _R._EvaluationsCompletedThisSession / dt;
		var title = $"Seed: {_RandomSeed}" +
			$" -- Iters: {iters}" +
			$" -- Evals new: {_R._EvaluationsCompletedThisSession} / cache: {_R._EvaluationsCompletedByCache}" +
			$" ({frac:0.0} evals/s after {dt:0}s)" +
			$" -- Best: {_BestLoss:0.00}";
		lock (Console.Out) {
			Console.Title = title;
		}
	}

	public RunSimplex (int? seed)
	{
		Console.WriteLine ("Hyperparameter test");
		Process.GetCurrentProcess ().PriorityClass = ProcessPriorityClass.Idle;

		_Data = new SharedData ();
		_R = new (_Data, new SharedResults ());

		if (!seed.HasValue) {
			Console.Write ("Seed?: ");
			seed = int.Parse (Console.ReadLine ());
			Console.WriteLine ();
		}
		_RandomSeed = seed;
		_T0 = DateTime.UtcNow;

		UpdateTitle ();
		new Thread (() => {
			while (true) {
				Thread.Sleep (1000);
				UpdateTitle ();
			}
		}) {
			Name = "Console Title"
		}.Start ();

		SharedData.StartMemoryPrinterThread ();

		var guess = new WormyTreeCreationMeta ().GetParams ().ToArray ();

		/*
		var rr = new Random (_RandomSeed.Value);
		for (var i = 0; i < guess.Length; i++) {
			var min = -1000;
			var max = 1000;
			guess [i] = min + (max - min) * rr.NextDouble ();
		}
		*/

		var simplex = new NelderMead (numberOfVariables: guess.Length) {
			Function = ps => {
				Console.WriteLine (string.Join (", ", ps.Select (p => $"{p:0.0}")));
				var loss = _R.RunConfigWithLoss (ps);
				_BestLoss = Math.Min (_BestLoss, loss);
				UpdateTitle ();
				return loss;
			},
			Solution = guess,
		};
		for (var i = 0; i < guess.Length; i++) {
			simplex.StepSize [i] = float.Parse ($"0.{seed}0");
		}

		var success = simplex.Minimize ();

		Console.WriteLine ("Success: " + success);
		Console.WriteLine ("Minimum at: " + string.Join (", ", simplex.Solution));
		Console.WriteLine ("Function value: " + simplex.Value);
	}
}
