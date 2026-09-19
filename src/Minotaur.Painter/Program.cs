using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using Minotaur.Boards.Common;
using Minotaur.Boards.Dancing;
using Minotaur.Boards.Drawing;
using Minotaur.Boards.Strings;

namespace Minotaur.Painter;

public class Program
{
	public static void Main ()
	{
		var root = "img";
		var files = Directory.GetFiles (root, "*.board", SearchOption.AllDirectories);
		foreach (var file in files) {
			Paint (file);
		}
	}

	private static void Paint (string inputPath)
	{
		Console.WriteLine ($"Draw board from file: {inputPath}");
		var outPathWithoutExtension = Path.ChangeExtension (inputPath, null);
		var rawLines = File.ReadAllLines (inputPath);
		Paint (rawLines, outPathWithoutExtension, null);
	}

	private static void Paint (string [] rawLines, string outPathWithoutExtension, string sub)
	{
		WormyBoardDrawerInstance inst = null;
		WormyNode tn = null;

		int CellFromIndex (string index)
		{
			if (index.Contains (",")) {
				var split = index.Split (',');
				var y = int.Parse (split [0]);
				var x = int.Parse (split [1]);
				var sx = tn.Grid.SX;
				var i = y * sx + x;
				return i;
			}
			else {
				return int.Parse (index);
			}
		}

		var colors = new Dictionary<string, Color> ();
		void addColor (string colorName, string colorHtmlCode)
		{
			var col = ColorTranslator.FromHtml ($"#{colorHtmlCode}");
			colors.Add (colorName, col);
		}
		// Some predefined names
		addColor ("blue", "b7d8ff");
		addColor ("green", "b5ffaa");
		addColor ("red", "ffaea8");
		addColor ("brown", "e6c19a");
		addColor ("white", "ffffff");
		addColor ("magenta", "dbc3d3");

		for (int iLine = 0; iLine < rawLines.Length; iLine++) {
			try {
				var rawLine = rawLines [iLine];
				var split = rawLine.Split (' ', 2);

				switch (split [0]) {
					case "":
					case "#":
						break;

					case "sub":
						var remainingLines = rawLines.Skip (iLine + 1).ToArray ();
						iLine = rawLines.Length;
						var subName = " " + split [1];
						Paint (remainingLines, outPathWithoutExtension, subName);
						break;

					case "board": {
						var grid = split [1];

						if (1111 == 111) {
							var ss = Solver.Solved (grid);
							var rows = ss.Matrix.M.RowsToString ();
							Console.WriteLine (rows);
						}

						var b = new BoardGrid (grid);

						if (1111 == 111) {
							var img2 = BoardGridDrawer.AsImage (b, "Custom paint");
							img2.Save ($"{outPathWithoutExtension}{sub} dance.png", ImageFormat.Png);
						}

						var meta = new WormyTreeCreationMeta () {
							allowDecisions = true,
							checkEndCondition = false,
							allowBoardsplit = false,
							allowChoices = false,
							allowBottlenecks = false,
							allowPieceOperation = false,
							prefilterDecisions = false,
						};
						var wb = new WormyBoardGrid (b);
						tn = new WormyNode (meta, wb);
					}
					break;

					case "inst": {
						var drawer = new WormyBoardDrawerCommon (tn.Grid, false);
						var imgpath = $"{outPathWithoutExtension}{sub}";
						inst = new WormyBoardDrawerInstance (
							drawer,
							imgpath,
							tn,
							new WormyAction [] { });

						inst.DrawPng = false;
						inst.DrawSvg = false;
						inst.DrawTikz = true;
					}
					break;

					case "cell": {
						split = split [1].Split (' ', 2);
						int i = CellFromIndex (split [0]);
						var name = split [1];
						inst.CellNames.Add (i, name);
					}
					break;

					case "defcol": {
						split = split [1].Split (' ');
						addColor (split [1], split [0]);
					}
					break;

					case "col": {
						split = split [1].Split (' ');
						var name = split [0];
						var col = colors [name];
						split = split.Skip (1).ToArray ();

						if (split [0] == "all") {
							var allCells = tn.Grid.AllWorms
								.SelectMany (w => w.Cells)
								.ToArray ();
							split = allCells
								.Select (c => c.ToString ())
								.ToArray ();
						}

						foreach (var si in split) {
							int i = CellFromIndex (si);
							inst.CellColors.Add (i, col);
						}
					}
					break;

					case "connectible-green": {
						inst.ConnectiblesWhite = false;
					}
					break;

					case "auto-name": {
						inst.AutoName = true;
					}
					break;

					case "fade": {
						switch (split [1]) {
							case "east":
								inst.FadeEast = true;
								break;
							case "west":
								inst.FadeWest = true;
								break;
							case "north":
								inst.FadeNorth = true;
								break;
							case "south":
								inst.FadeSouth = true;
								break;
							default:
								Console.Error.WriteLine (rawLine);
								break;
						}
					}
					break;

					case "name":
						inst.Path = $"{Path.GetDirectoryName (outPathWithoutExtension)}{sub}/{split [1]}";
						break;

					case "draw":
						if (split.Length > 1) {
							split = split [1].Split (' ');
							if (split.Contains ("png")) {
								inst.DrawPng = true;
							}
						}
						inst.Draw ();
						inst = null;
						break;

					case "merge":
					case "split": {
						var isMerge = split [0] == "merge";
						split = split [1].Split (' ');

						if (split [0] == "all") {
							var allCells = tn.Grid.AllWorms
								.SelectMany (w => w.Cells)
								.ToArray ();
							split = allCells
								.Select (c => c.ToString ())
								.ToArray ();
						}

						var w0 = CellFromIndex (split [0]);
						var mergeWith = split [1..]
							.Select (i => CellFromIndex (i))
							.ToList ();
						split = null;

						for (int iter = mergeWith.Count; iter > 0; --iter) {
							tn.Expand ();
							bool found = false;

							foreach (var childPiv in tn.AllTreeChildren) {
								if (childPiv is not WormyPivot_Decision piv) {
									continue;
								}
								// Which worms were merged/split here?
								var nbs = piv.MergedWorms.ToList ();
								// Is the origin part of them?
								var me = nbs.FirstOrDefault (nb => nb.Cells.Contains (w0));
								if (me == null) {
									continue;
								}
								nbs.Remove (me);
								Debug.Assert (nbs.Count == 1);
								// Is the other worm one we should merge/split?
								var other = nbs.Single ();
								var mergeCell = other.Cells.FirstOrDefault (c => mergeWith.Contains (c), -1);
								if (mergeCell == -1) {
									// No!
									continue;
								}
								// Yes!
								found = true;
								mergeWith.Remove (mergeCell);
								tn.ForcePrune (p => p != piv);
								piv.Expand ();
								break;
							}

							if (!found) {
								Console.Error.WriteLine ($"Could not find strings to merge: {tn.Grid.WormInCell [w0]} with {string.Join ("; ", mergeWith)}");
								break;
							}
							Debug.Assert (tn.Pivots.Count () == 1);
							var childActions = tn
								.Pivots
								.Single ()
								.ChildActions;
							WormyAction next;
							if (isMerge) {
								next = childActions.OfType<WormyAction_Merge> ().Single ();
							}
							else {
								next = childActions.OfType<WormyAction_Separation> ().Single ();
							}

							next.Expand (); // Refers to the action, not the pivot
							tn = (WormyNode) next.Child;
							//tn.Grid.PrintSelf ();
						}
					}
					break;

					default:
						Console.Error.WriteLine ($"Unknown command in line {iLine}: {rawLine}");
						break;
				}
			}
			catch (Exception ex) {
				Console.Error.WriteLine ($"Error on line {iLine}: {rawLines [iLine]}\n{ex}");
			}
		}
	}
}
