using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace Minotaur.Boards.Drawing.Generic;

public class SvgDrawer : GenericDrawer
{
	private StringBuilder _SB;

	public SvgDrawer ((int _pSX, int _pSY) size)
	{
		_SB = new ();

		_SB.Append ($"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
		_SB.Append ($"<!DOCTYPE svg PUBLIC \"-//W3C//DTD SVG 1.1//EN\" \"http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd\">\n");
		_SB.Append ($"<svg\n");
		_SB.Append ($"	version=\"1.1\"\n");
		_SB.Append ($"	xmlns=\"http://www.w3.org/2000/svg\"\n");
		_SB.Append ($"	xmlns:xlink=\"http://www.w3.org/1999/xlink\"\n");
		_SB.Append ($"	xmlns:xml=\"http://www.w3.org/XML/1998/namespace\"\n");
		_SB.Append ($"	width=\"{size._pSX}\"\n");
		_SB.Append ($"	height=\"{size._pSY}\"\n");
		_SB.Append ($">\n");
		_SB.Append ($"\n");

		_SB.Append ($"<pattern id=\"diagHatchUp\" patternUnits=\"userSpaceOnUse\" width=\"4\" height=\"4\">\n");
		_SB.Append ($"<path d=\"\n");
		_SB.Append ($" M0,4 l4,-4\n");
		_SB.Append ($" M-1,1 l2,-2\n");
		_SB.Append ($" M3,5 l2,-2\n");
		_SB.Append ($"\" style=\"stroke:black; stroke-width:0.4\" />\n");
		_SB.Append ($"</pattern>\n");

		_SB.Append ($"<pattern id=\"diagHatchDown\" patternUnits=\"userSpaceOnUse\" width=\"4\" height=\"4\">\n");
		_SB.Append ($"<path d=\"\n");
		_SB.Append ($" M0,0 l4,4\n");
		_SB.Append ($" M-1,3 l2,2\n");
		_SB.Append ($" M3,-1 l2,2\n");
		_SB.Append ($"\" style=\"stroke:black; stroke-width:0.4\" />\n");
		_SB.Append ($"</pattern>\n");

		_SB.Append ($"<pattern id=\"smallConfetti\" patternUnits=\"userSpaceOnUse\" width=\"4\" height=\"4\">\n");
		_SB.Append ($"<path d=\"\n");
		_SB.Append ($"M0.0,3.5 l1.0,-0.2\n");
		_SB.Append ($"M0.2,1.4 l0.2,1.1\n");
		_SB.Append ($"M1.0,0.2 l0.8,0.4\n");
		_SB.Append ($"M2.1,1.7 l-0.3,0.9\n");
		_SB.Append ($"M3.0,1.4 l0.7,-0.7\n");
		_SB.Append ($"M2.5,3.0 l0.7,0.7\n");
		_SB.Append ($"\" style=\"stroke:black; stroke-width:0.3\" />\n");
		_SB.Append ($"</pattern>\n");

		_SB.Append ($"\n");
	}

	public SvgDrawer (string fromFile)
	{
		var raw = File
			.ReadAllText (fromFile)
			.Replace ("\r\n", "\n");

		const string suffix = "</svg>\n";
		if (!raw.EndsWith (suffix)) {
			throw new ArgumentException ("The specified file uses an unknown format. The SvgDrawer class may by overly picky.");
		}

		_SB = new (raw, 0, raw.Length - suffix.Length, 0);
		_SB.Append ($"\n");
	}

	public override void DrawAndFinish ()
	{
		_Draws.Sort ();
		foreach (var tup in _Draws) {
			tup.a ();
		}
		_SB.Append ("</svg>\n");
	}

	public override void SaveAs (string path)
	{
		if (!path.EndsWith (".svg")) {
			path = $"{path}.svg";
		}

		/*
		 * Note: The UTF-8 BOM must NOT be used here: Files with BOM cannot be read by Graphviz
		 */
		using var sw = new StreamWriter (path);
		sw.Write (_SB.ToString ());
	}

	public override void DrawRect (
		int prio,
		Rectangle rect,
		Color color,
		int thickness)
	{
		Action a = () => {
			_SB.Append ($"<rect");
			_SB.Append ($" x=\"{rect.X}\"");
			_SB.Append ($" y=\"{rect.Y}\"");
			_SB.Append ($" width=\"{rect.Width}\"");
			_SB.Append ($" height=\"{rect.Height}\"");
			_SB.Append ($" fill=\"none\"");

			var scol = String.Format ("#{0}", color.ToArgb ().ToString ("x8").Substring (2));
			_SB.Append ($" stroke=\"{scol}\"");
			_SB.Append ($" stroke-width=\"{thickness}\"");

			_SB.Append ($" />\n");
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
		// TODO: fade
		void printRect (string fill)
		{
			_SB.Append ($"<rect");
			_SB.Append ($" x=\"{rect.X}\"");
			_SB.Append ($" y=\"{rect.Y}\"");
			_SB.Append ($" width=\"{rect.Width}\"");
			_SB.Append ($" height=\"{rect.Height}\"");
			_SB.Append ($" fill=\"{fill}\"");
			_SB.Append ($" />\n");
		}

		Action a = () => {
			var scol = String.Format ("#{0}", color.ToArgb ().ToString ("x8").Substring (2));
			printRect (scol);

			if (style.HasValue) {
				switch (style.Value) {
					case HatchStyle.ForwardDiagonal:
						printRect ("url(#diagHatchUp)");
						break;
					case HatchStyle.BackwardDiagonal:
						printRect ("url(#diagHatchDown)");
						break;
					case HatchStyle.SmallConfetti:
						printRect ("url(#smallConfetti)");
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
		// TODO escapeString
		// TODO internalName

		Action a = () => {
			var scol = string.Format ("#{0}", color.ToArgb ().ToString ("x8").Substring (2));

			switch (align) {
				case ContentAlignment.TopLeft:
					_SB.Append ($"<text");
					_SB.Append ($" x=\"{rect.X}\"");
					_SB.Append ($" y=\"{rect.Y}\"");
					sharedInsertText (s, font, scol);
					break;

				case ContentAlignment.MiddleCenter:
					_SB.Append ($"<svg");
					_SB.Append ($" x=\"{rect.X}\"");
					_SB.Append ($" y=\"{rect.Y}\"");
					_SB.Append ($" width=\"{rect.Width}\"");
					_SB.Append ($" height=\"{rect.Height}\"");
					_SB.Append ($">\n");

					_SB.Append ($"<text");
					_SB.Append ($" x=\"50%\"");
					_SB.Append ($" y=\"50%\"");
					_SB.Append ($" dominant-baseline=\"central\"");
					_SB.Append ($" text-anchor=\"middle\"");

					sharedInsertText (s, font, scol);

					_SB.Append ($"</svg>\n");
					break;

				default:
					throw new Exception ();
			}
		};
		AddAction (prio, a);

		void sharedInsertText (string s, Font font, string scol)
		{
			_SB.Append ($" style=\"white-space: pre;\"");
			_SB.Append ($" fill=\"{scol}\"");
			//_SB.Append ($" font-family=\"monospace\"");
			_SB.Append ($" font-family=\"{font.Name}\"");
			_SB.Append ($" font-size=\"{font.Size + 2}\""); // more because GDI font sizes are rendered bigger than they should

			_SB.Append ($">");

			if (s.Contains ("\n")) {
				var lines = s.Split ('\n');
				foreach (var rawLine in lines) {
					var line = rawLine.Length > 0 ? rawLine : " "; // empty tspans are ignored
					_SB.Append ($"<tspan x=\"0\" dy=\"1.1em\">{line}</tspan>\n");
				}
			}
			else {
				_SB.Append ($"{s}");
			}

			_SB.Append ($"</text>\n");
		}
	}
}
