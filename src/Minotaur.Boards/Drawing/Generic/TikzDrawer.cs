using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace Minotaur.Boards.Drawing.Generic;

public class TikzDrawer : GenericDrawer
{
	private StringBuilder _SB;

	public TikzDrawer ((int _pSX, int _pSY) size)
	{
		_SB = new ();

		_SB.Append ($"\\begin{{scope}}\n");
		_SB.Append ($"[\n");
		_SB.Append ($"yscale = -1,\n");
		_SB.Append ($"]\n");
		_SB.Append ($"{{\n");
	}

	public override void DrawAndFinish ()
	{
		_Draws.Sort ();
		foreach (var tup in _Draws) {
			tup.a ();
		}
		_SB.Append ($"}};\n");
		_SB.Append ($"\\end{{scope}}\n");
	}

	public override void SaveAs (string path)
	{
		path = $"{path}.tikz";
		using var sw = new StreamWriter (path);
		sw.Write (_SB.ToString ());
	}

	private readonly HashSet<Color> _Colors = new ();

	private string DefineColor (Color col)
	{
		var cname = $"r{col.R}g{col.G}b{col.B}";
		if (!_Colors.Contains (col)) {
			_Colors.Add (col);
			Action a = () => {
				_SB.Append ($"\\definecolor{{");
				_SB.Append ($"{cname}");
				_SB.Append ($"}}");
				_SB.Append ($"{{RGB}}");
				_SB.Append ($"{{");
				_SB.Append ($"{col.R},{col.G},{col.B}");
				_SB.Append ($"}};\n");
			};
			AddAction (WormyBoardDrawerInstance.PRIO_PREAMBLE, a);
		}
		return cname;
	}

	public override void DrawRect (
		int prio,
		Rectangle rect,
		Color color,
		int thickness)
	{
		var cname = DefineColor (color);

		Action a = () => {
			_SB.Append ($"\\draw");
			_SB.Append ($" [");
			_SB.Append ($"draw = {cname},");
			_SB.Append ($"line width = {thickness}pt,");
			_SB.Append ($"]");
			_SB.Append ($" ({rect.X}pt,{rect.Y}pt)");
			_SB.Append ($" rectangle");
			_SB.Append ($" +({rect.Width}pt,{rect.Height}pt)");
			_SB.Append ($";\n");
		};
		AddAction (prio, a);
	}

	public override void FillRect (
		int prio,
		Rectangle rect,
		Color color,
		HatchStyle? style,
		string fade)
	{
		var cname = DefineColor (color);

		void printRect ()
		{
			_SB.Append ($"\\fill");
			_SB.Append ($" [");
			_SB.Append ($"fill = {cname},");
			if (fade != null) {
				_SB.Append ($"path fading={fade},");
			}
			_SB.Append ($"]");
			_SB.Append ($" ({rect.X}pt,{rect.Y}pt)");
			_SB.Append ($" rectangle");
			_SB.Append ($" +({rect.Width}pt,{rect.Height}pt)");
			_SB.Append ($";\n");
		}

		Action a = () => {
			printRect ();

			if (style.HasValue) {
				switch (style.Value) {
					// TODO see https://tikz.dev/tikz-arrows#sec-16.3.7
					case HatchStyle.ForwardDiagonal:
						//TODO printRect ("url(#diagHatchUp)");
						break;
					case HatchStyle.BackwardDiagonal:
						//TODO printRect ("url(#diagHatchDown)");
						break;
					case HatchStyle.SmallConfetti:
						//TODO printRect ("url(#smallConfetti)");
						break;
					default:
						throw new Exception ("Unknown hatch style.");
				}
			}
		};
		AddAction (prio, a);
	}

	public override void DrawString (
		int prio,
		string s,
		bool escapeString,
		string internalName,
		Font font,
		Color color,
		Rectangle rect,
		ContentAlignment align)
	{
		if (escapeString) {
			s = s
				.Replace ("\\", "\\\\")
				.Replace ("_", "\\_");
		}
		Action a = () => {
			_SB.Append ($"\\path");
			_SB.Append ($" ({rect.X}pt,{rect.Y}pt)");
			_SB.Append ($" rectangle");
			_SB.Append ($" +({rect.Width}pt,{rect.Height}pt)");

			switch (align) {
				case ContentAlignment.TopLeft:
					_SB.Append ($" node [pos = .0, anchor = north west]");
					break;

				case ContentAlignment.MiddleCenter:
					_SB.Append ($" node [pos = .5]");
					break;

				default:
					throw new Exception ();
			}
			_SB.Append ($" ({internalName})");
			_SB.Append ($" {{");

			_SB.Append ($"\\begingroup");
			var fontSize = s.Length <= 1
				? font.Size * 1.6
				: font.Size * 1.25;
			var lineDist = font.Size * 1.15;
			_SB.Append ($"\\fontsize{{{fontSize:0.0}pt}}{{{lineDist:0.0}pt}}\\selectfont");

			if (s.StartsWith ("$") || s.StartsWith ("\\")) {
				// math mode or manual mode selection
			}
			else {
				// use \texttt by default
				_SB.Append ($"\\texttt");
			}
			_SB.Append ($"{{");
			_SB.Append ($"{s}");
			_SB.Append ($"}}");

			_SB.Append ($"\\endgroup");

			_SB.Append ($"}}");
			_SB.Append ($";\n");
		};
		AddAction (prio, a);
	}
}
