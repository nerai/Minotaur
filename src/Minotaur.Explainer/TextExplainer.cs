using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Minotaur.Boards.Common;
using Minotaur.Boards.Dancing;
using Minotaur.Boards.Drawing;
using Minotaur.Boards.Strings;

namespace Minotaur.Explanation;

public class TextExplainer<TGrid> where TGrid : ISolutionGrid
{
	private readonly Explainer2<TGrid> _E;
	private readonly string _Root;

	private readonly WormyBoardDrawerCommon _Drawer;
	private readonly List<TreeAction<TGrid>> _Assumptions = new ();
	private readonly StreamWriter _Html;
	private readonly BlockingCollection<Action> _QWriteImages;
	private readonly Thread _TWriteImages;

	public TextExplainer (
		Explainer2<TGrid> source,
		string root)
	{
		_E = source;
		_Root = root;

		_Drawer = new (_E._Root.Grid, false);

		Console.WriteLine ($"Explaining flat in root {root}");
		Directory.CreateDirectory (root);

		_Html = new StreamWriter ($"{root}/index.html");
		_Html.WriteLine ("<!DOCTYPE html>");
		_Html.WriteLine ("<html>");
		_Html.WriteLine ("");
		_Html.WriteLine ("<head>");
		_Html.WriteLine ("");

		_Html.WriteLine ($"<title>Explain {Path.GetFileNameWithoutExtension (root)}</title>");

		_Html.WriteLine ("<style>");
		var style = File.ReadAllText ("default.css");
		_Html.WriteLine (style);
		_Html.WriteLine ("</style>");

		_Html.WriteLine ("");
		_Html.WriteLine ("<body>");
		/*
		_Html.WriteLine ($"<div class='Init'>");
		_Html.WriteLine ($"<div>Explain {root}</div>");
		_Html.WriteLine ($"<div>Generated {DateTime.UtcNow:R}</div>");
		_Html.WriteLine ($"</div>");
		*/
		_Html.WriteLine ("<div class='steplist'>");

		(_QWriteImages, _TWriteImages) = WormyBoardDrawerCommon.CreateBackgroundDrawingQueue ("Explainer2::ExplainFlat");

		ExplainFlatString (_E._NodeLookup [_E._Root]);

		Console.WriteLine ($"HTML complete, waiting for image writer...");
		_QWriteImages.CompleteAdding ();
		_TWriteImages.Join ();
		Console.WriteLine ($"Image writer finished.");

		_Html.WriteLine ($"</div>");

		//_Html.WriteLine ($"<img style='width: 100%;' src=\"{Path.GetRelativePath (root, _E._RootDir)}\">");

		_Html.Dispose ();
	}

	private int _CurrentStep = 0;
	private const string imageFileExt = "svg";

	private void WriteItem (ImgNodeInfo<TGrid> node, string text, WormyAction action)
	{
		_Html.WriteLine ($"<div class='step'>");

		if (node is ImgNodeInfo<WormyBoardGrid> w) {
			var relpath = $"{_CurrentStep++:000000}";
			var abspath = $"{_Root}\\{relpath}";
			_Html.Write ($"<div class='step-img-container'>");
			_Html.WriteLine ($"<img class='step-img' src=\"{relpath}.{imageFileExt}\">");
			// xxx ggf das in die img zeile: height="50"
			// das NICHT mit % und NICHT als style, sonst geht es nicht.
			// vllt geht es aber besser, wenn man den container verwendet?
			_Html.Write ($"</div>");
			void work ()
			{
				var inst = new WormyBoardDrawerInstance (
					_Drawer,
					abspath,
					(WormyNode) w.Node,
					w.Ancestors ());
				inst.CreateHighlights (action);
				inst.Draw ();
			}
			_QWriteImages.Add (work);
		}

		_Html.Write ($"<div class='step-caption'>");
		_Html.Write ($"	<span>Step {_CurrentStep},</span>");
		_Html.Write ($"	<span>branch <span class='pre'>{node.Name}</span></span>");
		_Html.Write ($"</div>");

		var ass = string.Join ("\n", _Assumptions.Select (a => $"<li>{a.DescriptionHtml ()}"));
		_Html.Write ($"<div class='step-ass'>");
		if (ass.Any ()) {
			_Html.Write ($"Current assumptions:<list>{ass}</list>");
		}
		_Html.Write ($"</div>");

		text = text.Replace ("\n", "<br>");
		_Html.WriteLine ($"<div class='step-pre'>{text}</div>");

		_Html.WriteLine ($"</div>");
	}

	private void ExplainFlatString (ImgNodeInfo<TGrid> current)
	{
		var debug_InitialNode = current;

		while (current != null) {
			if (current.Children.Count == 0) {
				Explain_Leaf (current);
				return;
			}

			if (current.Node.Grid.UnfinishedCells <= 10 && !current.Node.IsSolvable.Value) {
				Explain_UnsolvableBranch (current);
				return;
			}

			if (current.Children.Count == 1) {
				var a = current.Children.Single ();
				WriteItem (current, "", null);

				var s = a.Key.DescriptionHtml ();
				WriteItem (current, s, a.Key as WormyAction);
				current = a.Value;
				continue;
			}

			var children = current.Children
				.OrderBy (c => c.Value.Node.IsSolvable)
				.ThenBy (c => c.Value.Node.CountTreeNodes ())
				.ToList ();

			if (current.Node is DancingSolutionPivot piv) {
				current = Explain_Dancing (current);
				continue;
			}

			if (children.All (c => c.Key is WormyAction_Merge || c.Key is WormyAction_Separation)) {
				current = Explain_Worm_Decision (current);
				continue;
			}

			if (children.All (c => c.Key is WormyAction_Choice)) {
				current = Explain_Worm_Choice (current);
				continue;
			}

			if (children.All (c => c.Key is WormyAction_Piece)) {
				current = Explain_Worm_Pieces (current);
				continue;
			}

			throw new Exception ();
		}
	}

	private ImgNodeInfo<TGrid> Explain_Dancing (ImgNodeInfo<TGrid> current)
	{
		var children = current.Children
			.OrderBy (c => c.Value.Node.IsSolvable)
			.ThenBy (c => c.Value.Node.CountTreeNodes ())
			.ToList ();

		var choices = children.ToDictionary (c => c.Key as DancingSolutionAction, c => c.Value);
		var good = choices.Where (c => c.Key.Child.IsSolvable.Value);
		var bad = choices.Where (c => !c.Key.Child.IsSolvable.Value);
		var nSols = good.Count ();

		if (nSols == 0) {
			var s = $"This board cannot be solved. It has {children.Count} continuation(s).";
			WriteItem (current, s, null);
			s = "";

			foreach (var choice in choices) {
				ExplainFlatString (choice.Value);
			}

			s += $"Backing up, this shows that all pathes that branch from this node lead to a dead end.";
			WriteItem (current, s, null);
			s = "";

			return null;
		}
		else {
			var s = $"This board can be solved. It has {good.Count ()} solvable and {bad.Count ()} unsolvable continuation(s).";
			WriteItem (current, s, null);
			s = "";

			foreach (var choice in bad) {
				ExplainFlatString (choice.Value);
			}

			s += $"Backing up, there is a solvable continuation.";
			WriteItem (current, s, null);
			s = "";

			return good.Single ().Value;
		}
	}

	private void Explain_Leaf (ImgNodeInfo<TGrid> current)
	{
		var s = "";
		if (current.Node.IsSolution == true) {
			s += $"This board is solved.";
		}
		else {
			s += $"This board configuration cannot be solved";
			if (current.Node is WormyNode w) {
				s += $": ";
				s += w.GetUnsolvabilityInfoAsMultilineHtml ();
			}
		}
		WriteItem (current, s, null);
	}

	private void Explain_UnsolvableBranch (ImgNodeInfo<TGrid> current)
	{
		// TODO: eigentlich gilt das mehr oder weniger auch für beliebige kleine subspaces, nicht nur das globale brett
		// TODO: besser: pruefen wie viele unterschiedlichen fehlschlagsgruende es gibt, und wenn es nur einer ist, dann den anzeigen

		var s = "";
		s += $"This board cannot be solved.\n";

		var infos = new List<UnsolvabilityInfo> ();

		// xxx im moment nimmt das ALLE kinder
		// es sollte aber EINE gruppe von kindern waehlen und von nur denen dann die distinct gruende verwenden.
		// dazu sollte die gruppe gewaehlt werden die die besten distinct gruende hat.
		foreach (var sub in current.Node.EnumerateSubtreeDistinct ()) {
			infos.AddRange (sub.Info);
		}
		s += $"{UnsolvabilityInfo.Aggregate (infos)}\n";

		WriteItem (current, s, null);
	}

	private ImgNodeInfo<TGrid> Explain_Worm_Decision (ImgNodeInfo<TGrid> current)
	{
		var children = current.Children
			.OrderBy (c => c.Value.Node.IsSolvable)
			.ThenBy (c => c.Value.Node.CountTreeNodes ())
			.ToList ();

		var merge_is_correct = children [1].Key is WormyAction_Merge;
		var merge = children
			.Select (c => c.Key)
			.OfType<WormyAction_Merge> ()
			.Single ();

		var s = $"It is not immediately clear if [{merge.Pivot.ToShortString ()}] is connected to [{merge.Neighbour.ToShortString ()}].\n";
		s += TalkAboutThePivotChoice (current);
		WriteItem (current, s, null);
		s = "";

		s += $"Assume that they are {(merge_is_correct ? "not " : "")}connected.\n";
		WriteItem (current, s, merge);
		s = "";

		_Assumptions.Add (children [0].Key);
		ExplainFlatString (children [0].Value);
		_Assumptions.Remove (children [0].Key);

		var they = $"[{merge.Pivot.ToShortString ()}] and [{merge.Neighbour.ToShortString ()}]";
		s += $"Backing up, it is now established that it was wrong to assume that {they} are {(merge_is_correct ? "not " : "")}connected.\n";
		s += $"Accordingly, the opposite is forced.\n";
		WriteItem (current, s, merge);
		s = "";

		if (children.All (c => !c.Value.Node.IsSolvable.Value)) {
			ExplainFlatString (children [1].Value);

			s += $"Backing up, this shows that all pathes that branch from this node lead to a dead end.";
			WriteItem (current, s, merge);
			s = "";

			return null;
		}
		else {
			return children [1].Value;
		}
	}

	private ImgNodeInfo<TGrid> Explain_Worm_Choice (ImgNodeInfo<TGrid> current)
	{
		var children = current.Children
			.OrderBy (c => c.Value.Node.IsSolvable)
			.ThenBy (c => c.Value.Node.CountTreeNodes ())
			.ToList ();

		var choices = children.ToDictionary (c => c.Key as WormyAction_Choice, c => c.Value);

		var pivot = choices.First ().Key.Pivot;

		var s = $"String [{pivot.ToShortString ()}] has only {choices.Count} possible, mutually exclusive continuations:\n";
		s += $"<list class='options-list'>";
		foreach (var choice in choices) {
			s += $"<li>{choice.Key.DescriptionHtml ()}";
		}
		s += $"</list>";
		s += TalkAboutThePivotChoice (current);
		WriteItem (current, s, null);
		s = "";

		foreach (var choice in choices) {
			if (choice.Value.Node.IsSolvable.Value) {
				// This one will be last
				continue;
			}

			s += $"Assume that {choice.Key.DescriptionHtml ()}\n";
			WriteItem (current, s, choice.Key);
			s = "";

			var ass = choice.Key as TreeAction<TGrid>;
			_Assumptions.Add (ass);
			ExplainFlatString (choice.Value);
			_Assumptions.Remove (ass);
		}

		if (choices.All (c => !c.Value.Node.IsSolvable.Value)) {
			s += $"Backing up, this shows that all pathes that branch from this node lead to a dead end.";
			WriteItem (current, s, null);
			s = "";

			return null;
		}
		else {
			var correct = choices.Single (c => c.Value.Node.IsSolvable.Value);

			s += $"Backing up, only a single choice remains:\n";
			s += $"{correct.Key.DescriptionHtml ()}\n";
			WriteItem (current, s, correct.Key);
			s = "";

			return correct.Value;
		}
	}

	private ImgNodeInfo<TGrid> Explain_Worm_Pieces (ImgNodeInfo<TGrid> current)
	{
		var children = current.Children
			.OrderBy (c => c.Value.Node.IsSolvable)
			.ThenBy (c => c.Value.Node.CountTreeNodes ())
			.ToList ();

		var choices = children.ToDictionary (c => c.Key as WormyAction_Piece, c => c.Value);
		var pivot = (WormyPivot_Piece) choices.First ().Key.Parent;

		var s = $"The {pivot.UsedPiece.Name}-piece fits in only {choices.Count} spots.\n";
		s += $"<list class='options-list'>";
		foreach (var choice in choices) {
			s += $"<li>{choice.Key.DescriptionHtml ()}";
		}
		s += $"</list>";

		s += TalkAboutThePivotChoice (current);
		WriteItem (current, s, null);
		s = "";

		foreach (var choice in choices) {
			if (choice.Value.Node.IsSolvable.Value) {
				// This one will be last
				continue;
			}

			s += $"Assume that {choice.Key.DescriptionHtml ()}\n";
			WriteItem (current, s, choice.Key);
			s = "";

			var ass = choice.Key as TreeAction<TGrid>;
			_Assumptions.Add (ass);
			ExplainFlatString (choice.Value);
			_Assumptions.Remove (ass);
		}

		if (choices.All (c => !c.Value.Node.IsSolvable.Value)) {
			s += $"Backing up, this shows that all pathes that branch from this node lead to a dead end.";
			WriteItem (current, s, null);
			s = "";

			return null;
		}
		else {
			var correct = choices.Single (c => c.Value.Node.IsSolvable.Value);

			s += $"Backing up, only a single choice remains:\n";
			s += $"{correct.Key.DescriptionHtml ()}\n";
			WriteItem (current, s, correct.Key);
			s = "";

			return correct.Value;
		}
	}

	private string TalkAboutThePivotChoice (ImgNodeInfo<TGrid> current)
	{
		var children = current.Children
			.OrderBy (c => c.Value.Node.IsSolvable)
			.ThenBy (c => c.Value.Node.CountTreeNodes ())
			.ToList ();

		var choices = children.ToDictionary (c => c.Key as WormyAction, c => c.Value);

		var s = "";

		var pivNode = choices.Keys.First ().Parent;
		if (pivNode.Parent.PrunedChildren != null) {
			var notPivot = pivNode.Parent.PrunedChildren
				.OrderByDescending (ch => ch.Desire)
				.ToList ();
			if (notPivot.Count > 0) {
				s += $"\n";
				if (pivNode.Desire >= notPivot.First ().Desire) {
					s += $"According to the heuristic, this is the best pivot.";
				}
				else {
					s += $"According to the heuristic, this is NOT the best pivot.";
				}
				var otherGood = notPivot
					.TakeWhile (p => p.Desire >= pivNode.Desire * 0.8)
					.Cast<WormyPivot> ()
					.Take (4)
					.ToList ();
				if (otherGood.Count == 0) {
					s += $" There are no other good pivots.\n";
				}
				else {
					s += $" Other good pivots are:\n";
					s += $"<list class='options-list'>";
					foreach (var other in otherGood) {
						s += $"<li>{other.AsHtml ()}";
					}
					s += $"</list>";
				}
			}
		}

		return s;
	}
}
