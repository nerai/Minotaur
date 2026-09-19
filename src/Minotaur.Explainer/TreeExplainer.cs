using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading;
using Minotaur.Boards.Common;
using Minotaur.Boards.Drawing;
using Minotaur.Boards.Drawing.Generic;
using Minotaur.Boards.Strings;
using static Minotaur.Boards.Common.BoardGrid;

namespace Minotaur.Explanation;

public class TreeExplainer<TGrid> where TGrid : ISolutionGrid
{
	private readonly Explainer2<TGrid> _E;

	public TreeExplainer (
		Explainer2<TGrid> source,
		bool largeImage,
		Dictionary<int, Color> cellColors
		)
	{
		_E = source;

		/*
		 * Draw inner images
		 */
		Directory.CreateDirectory ($"{_E._RootDir}\\inner");
		var (qWriteImages, tWriteImages) = WormyBoardDrawerCommon.CreateBackgroundDrawingQueue ("Explainer2::CreateImageOfSolutionTreeGraphviz");
		var drawer = new WormyBoardDrawerCommon (_E._Root.Grid, largeImage);

		var usedColors = "";
		for (int i = 0; i < drawer._CellColor.Count; i++) {
			var col = drawer._CellColor [i];
			usedColors += $"{i,2}: {col.R,3}, {col.G,3}, {col.B,3}\n";
		}
		File.WriteAllText (
			$"{_E._RootDir}\\default_cell_colors.txt",
			usedColors);

		foreach (var pi in _E._NodeLookup.Values) {
			if (pi is ImgNodeInfo<WormyBoardGrid> w) {
				var imgpath = $"{_E._RootDir}\\inner\\{pi.Name.Replace ('/', ',')}";
				void work ()
				{
					var inst = new WormyBoardDrawerInstance (
						drawer,
						imgpath,
						(WormyNode) w.Node,
						w.Ancestors ());
					foreach (var k in cellColors.Keys) {
						inst.CellColors [k] = cellColors [k];
					}
					inst.AutoName = true;
					inst.Draw ();
				}
				qWriteImages.Add (work);
			}
		}

		//Console.WriteLine ($"Inner image enumeration complete, waiting for image writer...");
		qWriteImages.CompleteAdding ();
		tWriteImages.Join ();
		//Console.WriteLine ($"Image writer finished.");

		/*
		 * Draw outer files
		 */
		CreateImageOfSolutionTreeTikz (drawer, false, false, false, false);
		CreateImageOfSolutionTreeTikz (drawer, true, false, false, false);
		CreateImageOfSolutionTreeTikz (drawer, true, true, true, false);
		CreateImageOfSolutionTreeTikz (drawer, true, true, true, true);
		var imgFile = CreateImageOfSolutionTreeGraphviz (drawer, "png", "png", "-Tpng");
		//CreateImageOfSolutionTreeGraphviz (drawer, "png", "svg", "-Tpng");
		CreateImageOfSolutionTreeGraphviz (drawer, "svg", "png", "-Tsvg");
		CreateImageOfSolutionTreeGraphviz (drawer, "svg", "svg", "-Tsvg");
		CreateImageOfSolutionTreeGraphviz (drawer, "pdf", "png", "-Tpdf");
		//CreateImageOfSolutionTreeGraphviz (drawer, "pdf", "svg", "-Tpdf");

		var dirname = Path.GetDirectoryName (imgFile);
		var newfilename = Path.GetFileName (dirname);
		newfilename = $"{dirname}/../{newfilename}.png";
		File.Copy (imgFile, newfilename, true);
	}

	private void CreateImageOfSolutionTreeTikz (
		WormyBoardDrawerCommon drawer,
		bool drawImage,
		bool writeCost,
		bool writeName,
		bool writeDebug)
	{
		var texts = new [] {
			(writeCost ? "cost" : null),
			(writeName ? "name" : null),
			(writeDebug ? "debug" : null),
		};
		var stexts = string.Join (",", texts);
		var pathMain = $"{_E._RootDir}\\tree img={(drawImage ? 1 : 0)} write=[{stexts}].tex";
		Directory.CreateDirectory (Path.GetDirectoryName (pathMain));
		var saveboxPrefix = $"T{(ulong) pathMain.GetHashCode ()}-";

		using (var write = new StreamWriter (pathMain)) {
			write.Write ($"% Latex tree: {pathMain}\n\n");

			write.Write ("\n% --- References graphics ---\n\n");
			foreach (var pi in _E._NodeLookup.Values) {
				write.Write ("\\xsbox{");
				write.Write (saveboxPrefix);
				write.Write (pi.Name);
				write.Write ("}{%\n");
				write.Write ("\\begin{tikzpicture}%\n");

				var imgpath = $"\\currfiledir/inner/{pi.Name.Replace ('/', ',')}.tikz";
				write.Write ("\\input{");
				write.Write (imgpath);
				write.Write ("}%\n");

				write.Write ("\\end{tikzpicture}%\n");
				write.Write ("}\n\n");
			}

			write.Write ("\n% --- Graph ---\n\n");

			write.Write ("\\begin{tikzpicture} [\n");
			write.Write ("]\n");
			write.Write ("\n");

			write.Write ("\\begin{scope} [\n");

			// It is a tree
			write.Write ("tree layout,\n");

			// Important for distance between initial column and regular tree
			write.Write ("component sep = 5em,\n");

			// Distance between "rows" (this is later overriden sometimes)
			write.Write ("level sep = 1.2em,\n");

			// Distance between centers(!) of nodes is meaningless
			write.Write ("node distance = 0,\n");
			write.Write ("sibling distance = 0,\n");

			// Distance between node edges is important
			write.Write ("node sep = 0,\n");
			write.Write ("sibling sep = 1.0em,\n");
			write.Write ("significant sep = 3.0em,\n");

			write.Write ("]\n");
			write.Write ("\n");

			var addAfterGraph = "";

			foreach (var pi in _E._NodeLookup.Values) {
				var it = pi.Node;
				var s =
					it.IsSolution == true ? "solved" :
					!it.Pivots.Any () ? "fail" :
					it.IsSolvable.Value ? "[+]" :
					"[-]";
				if (s == "fail") {
					s = it.GetUnsolvabilityInfoAsMultiline ();
				}

				string lbl;
				if (pi is ImgNodeInfo<WormyBoardGrid> w) {
					lbl = "";
					lbl += $"\\setlength{{\\tabcolsep}}{{0pt}}";
					lbl += $"\\begin{{tabular}}{{c}}";
					lbl += $"\n";
					if (true) {
						Color fc;
						if (!it.PruningComplete) {
							fc = Color.FromArgb (120, 120, 120);
						}
						else if (it.IsSolution == true) {
							fc = Color.FromArgb (140, 255, 140);
						}
						else if (it.Pivots.Count () == 0) {
							fc = Color.FromArgb (255, 140, 140);
						}
						else if (it.IsSolvable.Value) {
							fc = Color.FromArgb (220, 255, 170);
						}
						else {
							fc = Color.FromArgb (255, 180, 235);
						}

						var col = ColorTranslator.ToHtml (fc) [1..];
						var lines = s
							.Split ('\n')
							.Select (line => {
								var s = "";
								s += $"\\cellcolor[HTML]{{{col}}}";
								s += "\\texttt{";
								s += line.EscapeLatex ();
								s += "}";
								return s;
							});
						lbl += string.Join ("\\\\", lines);
						lbl += $"\\\\\n";
					}
					if (drawImage || (pi.Parents.Count == 0) || (pi.Node.IsSolution == true)) {
						lbl += "\\vspace{0.1em}";
						lbl += "\\adjustbox{scale = 0.48}{";
						lbl += "\\xusebox{";
						lbl += saveboxPrefix;
						lbl += pi.Name;
						lbl += "}";
						lbl += "}";
						lbl += $"\\\\\n";
					}
					if (writeCost) {
						lbl += $"\\texttt{{";
						lbl += $"V: {it.Playouts}; ".EscapeLatex ();
						var costs = $"C: {it.MinCostFromRoot.AsUlong ()} + {it.LocalCost.AsUlong ()} + {it.MinCostToFinish.AsUlong ()}";
						lbl += costs.EscapeLatex ();
						lbl += $"}}";
						lbl += $"\\\\\n";
					}
					if (writeName) {
						lbl += $"\\small\\texttt{{";
						lbl += pi.Name.EscapeLatex ();
						lbl += $"}}";
						lbl += $"\\\\\n";
					}
					if (writeDebug) {
						var wn = pi.Node as WormyNode;
						if (wn != null && wn.PossiblePivots.HasValue) {
							lbl += $"\\texttt{{";
							var lim = wn.AppliedPivotLimit == int.MaxValue ? "+inf" : $"{wn.AppliedPivotLimit}";
							lbl += ($"Pivs: {wn.PossiblePivots} (limit {lim})").EscapeLatex ();
							lbl += $"}}";
							lbl += $"\\\\\n";
						}
						lbl += $"\\texttt{{";
						lbl += $"Pruned: {it.PrunedNodeCount:#,##0}".EscapeLatex ();
						lbl += $"}}";
						lbl += $"\\\\\n";
					}
					lbl += $"\\end{{tabular}}\n";
				}
				else {
					lbl = $"{it._DebugId}\\\\{pi.Name.EscapeLatex ()}";
				}

				write.Write ("\\node [");
				//write.Write ("draw, ");
				write.Write ("inner sep = 0, ");
				write.Write ("]\n");
				write.Write ($"(N{it._DebugId})\n");
				write.Write ("{");
				write.Write ("\\adjustbox{scale=0.5}{\n");
				write.Write (lbl);
				write.Write ("}");
				write.Write ("};\n\n");
			}

			const float PenThicknessSolvable = 4.5f;
			const float PenThicknessUnsolvable = 2.1f;

			foreach (var pi in _E._NodeLookup.Values) {
				var it = pi.Node;
				var thisIsTheFirstNodeWithBranches = pi.Children.Count > 1 && pi.AncestorsAreBranchless ();

				foreach (var pair in pi.Parents) {
					var a = pair.Key;
					var parentInfo = pair.Value;

					var e = $"E{parentInfo.Node._DebugId}to{it._DebugId}";

					write.Write ("\n\\node [");
					//write.Write ("draw,\n");
					write.Write ("inner sep = 0, ");
					if (pair.Key.Parent.AllTreeChildren.Count > 1) {
						/*
						 *  2: 3.0
						 *  3: 3.4
						 *  5: 4.0
						 * 10: 5.0
						 */
						var em = 3.0 + Math.Sqrt (pair.Key.Parent.AllTreeChildren.Count - 1) - 1;
						write.Write ($"level pre sep = {em:0.00}em, ");
					}
					write.Write ("] ");

					write.Write ($"({e}) ");

					var lbl = a.DescriptionLatex ();
					write.Write ("{");
					write.Write (lbl);
					write.Write ("};\n");

					var initing = it is WormyNode wn && wn.IsInitialitzingNearRoot ();
					var penthick = it.IsSolvable.Value
						? PenThicknessSolvable
						: PenThicknessUnsolvable;

					write.Write ($"\\path ");
					write.Write ($"(N{parentInfo.Node._DebugId})");
					write.Write ($" edge[--, ");
					write.Write ($"line width = {penthick}, ");
					write.Write ($"out = south, ");
					write.Write ($"in = north, ");
					write.Write ($"] ");
					write.Write ($"({e});\n");

					if (!thisIsTheFirstNodeWithBranches) {
						write.Write ($"\\path ");
						write.Write ($"({e})");
						write.Write ($" edge[--, line width = {penthick}] ");
						write.Write ($"(N{it._DebugId});\n");
					}
					else {
						addAfterGraph += ""
							+ $"\\draw [line width={PenThicknessSolvable}] "
							+ $"({e}.south)"
							+ $" --"
							+ $" ++(0,-1.2em)"
							+ $" --"
							+ $" ++(10.0em,0)"
							+ $" |-"
							+ $" ($(N{it._DebugId}.north) + (0,1.0em)$)"
							+ $" --"
							+ $" +(0,-1.0em);\n";
					}
				}
			}

			write.Write ("\\end{scope}\n");
			write.Write ("\n");
			write.Write (addAfterGraph);
			write.Write ("\n");
			write.Write ("\\end{tikzpicture}\n");
		}
		//Console.WriteLine ($"Tikz complete");
	}

	public static string GraphvizEscape (string s)
	{
		s = HtmlEncoder.Default.Encode (s);
		return s;
	}

	private string CreateImageOfSolutionTreeGraphviz (
		WormyBoardDrawerCommon drawer,
		string outerExt,
		string innerExt,
		string graphvizTcommand)
	{
		var swCommon = Stopwatch.StartNew ();

		var pathDot = $"{_E._RootDir}\\g_{innerExt}.dot"; // outerExt is independent
		Directory.CreateDirectory (Path.GetDirectoryName (pathDot));

		using (var write = new StreamWriter (pathDot)) {
			write.Write ("digraph G {\n"
				+ $"ranksep = 0.46\n"
				+ $"nodesep = 1.2\n"

				+ $""
				+ $"node ["
				+ $"fontname = \"Courier New\","
				+ $"fontsize = 14,"
				+ $"]\n"
				+ $""
				+ $"edge ["
				+ $"fontname = \"Courier New\","
				+ $"fontsize = 14,"
				+ $"]\n"
				);

			foreach (var pi in _E._NodeLookup.Values) {
				var it = pi.Node;
				var s =
					it.IsSolution == true ? "solved" :
					!it.Pivots.Any () ? "fail" :
					it.IsSolvable.Value ? "[+]" :
					"[-]";
				if (s == "fail") {
					s = it.GetUnsolvabilityInfoAsMultiline ();
				}

				string lbl;
				if (pi is ImgNodeInfo<WormyBoardGrid> w) {
					lbl = $"< <table cellspacing=\"0\" border=\"0\">";
					{
						Color fc;
						if (!it.PruningComplete) {
							fc = Color.FromArgb (120, 120, 120);
						}
						else if (it.IsSolution == true) {
							fc = Color.FromArgb (140, 255, 140);
						}
						else if (it.Pivots.Count () == 0) {
							fc = Color.FromArgb (255, 140, 140);
						}
						else if (it.IsSolvable.Value) {
							fc = Color.FromArgb (220, 255, 170);
						}
						else {
							fc = Color.FromArgb (255, 180, 235);
						}

						var col = ColorTranslator.ToHtml (fc);
						var escaped = GraphvizEscape (s);
						lbl += $"<tr><td bgcolor=\"{col}\">{escaped}</td></tr>";
					}
					{
						var imgpath = $"{_E._RootDir}\\inner\\{pi.Name.Replace ('/', ',')}";
						lbl += ""
							+ $"<tr><td>"
							+ $"<img"
							+ $" src=\"{imgpath}.{innerExt}\""
							+ $" scale=\"false\""
							+ $" />"
							+ $"</td></tr>";
					}
					{
						var wn = pi.Node as WormyNode;
						lbl += $"<tr><td>";
						lbl += GraphvizEscape ($"V: {it.Playouts}; ");
						lbl += GraphvizEscape ($"C: {it.MinCostFromRoot} + {it.LocalCost} + {it.MinCostToFinish}");
						lbl += $"<br />";
						if (wn != null && wn.PossiblePivots.HasValue) {
							var lim = wn.AppliedPivotLimit == int.MaxValue ? "+inf" : $"{wn.AppliedPivotLimit}";
							lbl += GraphvizEscape ($"Pivs: {wn.PossiblePivots} (limit {lim})");
							lbl += $"<br />";
						}
						lbl += GraphvizEscape ($"Pruned: {it.PrunedNodeCount:#,##0}");
						lbl += $"</td></tr>";
					}
					{
						var escaped = GraphvizEscape (pi.Name);
						lbl += $"<tr><td><font point-size=\"9.0\">{escaped}<br />N{pi.Node._DebugId}</font></td></tr>";
					}
					if (it.PrunedChildren?.Count > 0) {
						lbl += $"<tr><td><font point-size=\"6.0\">";
						lbl += GraphvizEscape (it.AllTreeChildren.Single ().ToString ());
						lbl += $"<br />";
						lbl += $"---<br />";
						lbl += string.Join ("<br />", it.PrunedChildren.Select (ch => GraphvizEscape (ch.ToString ())));
						lbl += $"</font></td></tr>";
					}
					lbl += $"</table> >";
				}
				else {
					lbl =
						$"\"" +
						$"{s}\n" +
						$"{pi.Name}" +
						$"\"";
				}

				write.Write ($"N{pi.Node._DebugId} [");
				write.Write ($"label = {lbl},");
				write.Write ($"shape = plain,");
				if (it.IsSolvable.Value) {
					write.Write ($"group = \"solvable\",");
				}
				write.Write ($"];\n");
			}

			foreach (var pi in _E._NodeLookup.Values) {
				var it = pi.Node;
				foreach (var pair in pi.Parents) {
					var a = pair.Key;
					var parentInfo = pair.Value;

					var e = $"E{parentInfo.Node._DebugId}_{it._DebugId}";

					write.Write ($"{e} [");
					var s = a.DescriptionConsole ();
					write.Write ($"label = \"{s}\",");
					write.Write ($"shape = plain,");
					if (it.IsSolvable.Value) {
						write.Write ($"group = \"solvable\",");
					}
					write.Write ($"];\n");

					var initing = it is WormyNode wn && wn.IsInitialitzingNearRoot ();
					write.Write ($"N{parentInfo.Node._DebugId} -> {e} [");

					var penthick = it.IsSolvable.Value ? 5f : 1.6f;
					write.Write ($"penwidth = {penthick},");

					write.Write ($"arrowsize = 0,");

					if (it.IsSolvable.Value) {
						write.Write ($"weight = 100,");
					}
					if (initing) {
						write.Write ($"constraint = false, color=\"#008000\",");
					}
					write.Write ($"];\n");

					write.Write ($"{e} -> N{it._DebugId} [");
					write.Write ($"penwidth = {penthick},");
					if (initing) {
						write.Write ($"constraint = false, color=\"#008000\",");
					}
					if (it.IsSolvable.Value && parentInfo.Children.Count > 1) {
						var siblings = parentInfo.Children.Values.Where (c => c != pi);
						/* XXX
						var maxdepth = siblings.Max (sib => sib.Node.MaxDepth);
						write.Write ($"minlen={maxdepth * 2 + 3},");
						*/
					}
					write.Write ($"];\n");
				}
			}

			write.Write ("}");
		}
		//Console.WriteLine ($"Dotfile complete");

		Console.WriteLine ($"Render {outerExt}/{innerExt}");
		var swRender = Stopwatch.StartNew ();
		var pathRawImg = $"{pathDot}.{outerExt}";

		var process = new Process ();
		/*
		 * NOTE: If this uses image files as input, ensure a fitting graphviz
		 * "loadimage" library was installed AND configured for dot.exe.
		 * --> "dot -c"
		 */
		var args = ""
			+ $" {graphvizTcommand}" // Something like -Tpng
			+ $" -o \"{pathRawImg}\""
			+ $" \"{pathDot}\"";
		process.StartInfo = new ProcessStartInfo (
			@"C:\dev_bin\graphviz\bin\dot.exe",
			//$" -v" +
			args
			) {
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true,
			WorkingDirectory = Path.GetDirectoryName (Path.GetFullPath (pathDot)),
		};
		process.Start ();

		void BackgroundReadStream (StreamReader stream, bool isError)
		{
			var reader = new Thread (() => {
				while (!process.HasExited) {
					var line = stream.ReadLine ();
					if (line == null) {
						break;
					}
					if (isError) {
						line = $"ERROR: {line}";
					}
					Console.WriteLine (line);
				}
			}) {
				Name = $"Console reader for graphviz {pathDot}",
				IsBackground = false,
				Priority = ThreadPriority.AboveNormal,
			};
			reader.Start ();
		}
		BackgroundReadStream (process.StandardOutput, false);
		BackgroundReadStream (process.StandardError, true);
		process.WaitForExit ();
		swRender.Stop ();

		/*
		 * Include additional information in final file.
		 * This is not required, just informational.
		 */
		var s1 = "";
		s1 += $"{DateTime.UtcNow:yyyy.MM.dd HH:mm}\n";
		s1 += $"{Path.GetFullPath (pathDot)}\n";
		s1 += $"Tree {swCommon.Elapsed.TotalSeconds:0.000}s\n";
		s1 += $"Render {swRender.Elapsed.TotalSeconds:0.000}s\n";

		var s2 = "";
		s2 += $"{_E._NodeLookup.Count} nodes, {_E._NodeLookup.Values.Sum (ini => ini.Children.Count)} edges\n";
		s2 += $"\n";
		s2 += $"{_E._Root.TextualRepresentation (TextualRepresentationStyle.Literals)}";

		while (true) {
			try {
				GenericDrawer g ;
				switch (outerExt) {
					case "png":
						try {
							g = new BitmapDrawer (pathRawImg);
						}
						catch (Exception ex) {
							// Maybe graphviz screwed up, would not be unusual.
							Console.WriteLine (ex.ToString ());
							// Ignore.
							g = null;
						}
						break;
					case "svg":
						g = new SvgDrawer (pathRawImg);
						break;
					case "pdf":
						// Not supported
						g = null;
						break;
					case "tikz":
						// Not supported
						g = null;
						break;
					default:
						throw new InvalidProgramException ($"Unknown file extension of {pathRawImg}");
				}
				if (g != null) {
					g.DrawString (0, s1, true, "renderInfo", ImgOutputShared._Font_Pre14, Color.Black, new Rectangle (20, 450, 0, 0), ContentAlignment.TopLeft);
					g.DrawString (0, s2, true, "nodeInfo", ImgOutputShared._Font_Pre30, Color.Black, new Rectangle (20, 580, 0, 0), ContentAlignment.TopLeft);
					g.DrawAndFinish ();
					g.SaveAs (pathRawImg);
				}
				break;
			}
			catch (Exception ex) {
				Console.Error.WriteLine (ex);
				Thread.Sleep (10000);
			}
		}

		//Console.WriteLine ("Graphviz complete");
		return pathRawImg;
	}
}
