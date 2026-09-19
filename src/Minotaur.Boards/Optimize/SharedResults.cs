using System.Diagnostics;
using System.Globalization;
using Minotaur.Boards.Strings;
using Minotaur.Utils;

namespace Minotaur.Boards.Optimize;

public class SharedResults
{
	public static readonly int ParamCount = new WormyTreeCreationMeta ().GetParams ().Count ();

	private readonly string _BaseDir = $@"Hyperparameter test for strings";
	private readonly string _AppendTsvFile;
	private List<Result> _Results = null;

	public SharedResults ()
	{
		_AppendTsvFile = $"{_BaseDir}/results/hyper_v4_{DateTime.UtcNow.Ticks}_{Process.GetCurrentProcess ().Id}.tsv";
		Directory.CreateDirectory (Path.GetDirectoryName (_AppendTsvFile));
		if (!File.Exists (_AppendTsvFile)) {
			File.WriteAllText (_AppendTsvFile, new WormyTreeCreationMeta ().LogHeader ());
		}
	}

	private void LoadResults ()
	{
		if (_Results != null) {
			return;
		}
		_Results = new List<Result> ();

		foreach (var file in Directory.GetFiles ($"{_BaseDir}/results")) {
			try {
				var lines = File.ReadAllLines (file).Skip (1);
				foreach (var line in lines) {
					var parts = line.Split ('\t');

					var sdate = parts [0];
					parts = parts.Skip (1).ToArray ();

					var ucost = ulong.Parse (parts [0], CultureInfo.InvariantCulture);
					parts = parts.Skip (1).ToArray ();

					var grid = parts [0];
					parts = parts.Skip (1).ToArray ();

					var param = new double [parts.Length];
					for (var i = 0; i < param.Length; i++) {
						param [i] = double.Parse (parts [i], CultureInfo.InvariantCulture);
					}

					var add = new Result (param, grid, ucost);
					_Results.Add (add);
				}
			}
			catch (Exception ex) {
				Logging.LogN ($"Failed to parse file {file}: {ex}");
			}
		}
		Logging.LogN ($"There are {_Results.Count} results");
	}

	public void LogTsv (Result res)
	{
		var line = string.Join ("\t", res.Param);
		line = $"{DateTime.UtcNow}\t{res.UCost}\t{res.Grid}\t{line}\n";

		while (true) {
			try {
				using var ff = File.Open (_AppendTsvFile, FileMode.Append, FileAccess.Write, FileShare.None);
				using var fs = new StreamWriter (ff);
				fs.Write (line);
				break;
			}
			catch (IOException ex) {
				Thread.Sleep (Random.Shared.Next (1, 10));
			}
		}
	}

	public List<Result> CopyResults ()
	{
		LoadResults ();
		lock (_Results) {
			return _Results.ToList ();
		}
	}

	public void AddResult (Result res)
	{
		if (_Results == null) {
			// Create list WITHOUT loading from disk
			_Results = new List<Result> ();
		}
		lock (_Results) {
			_Results.Add (res);
		}
	}
}
