using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Minotaur.Boards.Common;

public static class BoardGridDrawer
{
	private static ThreadLocal<Image> _ImageCellClosed = new ThreadLocal<Image> (() => Image.FromFile ("gfx/board_cell_closed3.png"));
	private static ThreadLocal<Image> _ImageCellOpen = new ThreadLocal<Image> (() => Image.FromFile ("gfx/board_cell_open2.png"));

	public record class ExportParameters (
		string Name,
		int SX,
		int SY,
		int CellSize,
		bool IsTransparent)
	{ }

	public static readonly ExportParameters Export_Screen = new (Name: "Screen", SX: 1650, SY: 1250, CellSize: 100, IsTransparent: false);
	public static readonly ExportParameters Export_DinA4 = new (Name: "DIN A4", 1024 * 104 / 100, 768 * 104 / 100, CellSize: 100, IsTransparent: true);
	public static readonly ExportParameters Export_Surface = new (Name: "Surface", SX: 2736, SY: 1824, CellSize: 258, IsTransparent: false);

	// Perceptually uniform CIElab, source: SH
	public static readonly Dictionary<char, Color> PieceColors = new () {
		{ 'F', Color.FromArgb (255, Color.FromArgb (0xFF5A50)) },
		{ 'I', Color.FromArgb (255, Color.FromArgb (0x3EC42D)) },
		{ 'L', Color.FromArgb (255, Color.FromArgb (0x3E94F7)) },
		{ 'N', Color.FromArgb (255, Color.FromArgb (0xF2E649)) },
		{ 'P', Color.FromArgb (255, Color.FromArgb (0x7DE34D)) },
		{ 'T', Color.FromArgb (255, Color.FromArgb (0x8BD4FE)) },
		{ 'U', Color.FromArgb (255, Color.FromArgb (0xFFA03C)) },
		{ 'V', Color.FromArgb (255, Color.FromArgb (0x29D7C3)) },
		{ 'W', Color.FromArgb (255, Color.FromArgb (0xA57EFF)) },
		{ 'X', Color.FromArgb (255, Color.FromArgb (0xB4C13E)) },
		{ 'Y', Color.FromArgb (255, Color.FromArgb (0xC47E31)) },
		{ 'Z', Color.FromArgb (255, Color.FromArgb (0xF7A0F8)) },
	};

	public static void CreateImageSelftest ()
	{
		var d = new List<(int, string)> ();
		d.Add ((0, "0"));
		d.Add ((1, "1"));
		d.Add ((2, "2"));
		d.Add ((3, "3"));
		d.Add ((4, "this is a very long line that goes on for like, really long, or something"));

		var c = new List<(int, Color)> ();
		c.Add ((0, Color.Red));
		c.Add ((1, Color.FromArgb (50, Color.Green)));
		c.Add ((2, Color.FromArgb (50, Color.Green)));
		c.Add ((2, Color.FromArgb (50, Color.Blue)));
		c.Add ((3, Color.FromArgb (50, Color.Blue)));

		var b = AsImage (
			new BoardGrid ("0100,0011,0001"),
			"desc",
			printText: d,
			printColor: c);
		b.Save ("test.png");
		return;
	}

	public static Bitmap AsImage (
		BoardGrid bg,
		string description,
		ExportParameters exportOptions = null,
		List<(int cell, Color col)> printColor = null,
		List<(int cell, string text)> printText = null,
		string description2 = "")
	{
		var parameters = exportOptions ?? Export_Screen;

		int cellSize = parameters.CellSize;
		int sx = parameters.SX;
		int sy = parameters.SY;

		var bmp = new Bitmap (sx, sy);
		var x0 = (sx - bg.SX * cellSize) / 2;
		var y0 = (sy - bg.SY * cellSize) / 2;

		Rectangle GetRect (int x, int y)
		{
			var rect = new Rectangle (
				x0 + x * cellSize,
				y0 + y * cellSize,
				cellSize,
				cellSize);
			return rect;
		}

		using (var g = Graphics.FromImage (bmp))
		using (var pen = new Pen (Color.Black, 2)) {
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.InterpolationMode = InterpolationMode.HighQualityBicubic;
			g.PixelOffsetMode = PixelOffsetMode.HighQuality;
			g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

			// Background is transparent, or white for tablet export
			if (!parameters.IsTransparent) {
				g.FillRectangle (Brushes.White, 0, 0, sx, sy);
			}

			// Draw all cells
			var draws = new List<(int priority, int counter, Action<Graphics> a)> ();
			const int PRIO_CLOSED_CELL = 10;
			const int PRIO_USED_CELL = 30;
			const int PRIO_EMPTY_CELL = 50;

			for (int y = -1; y < bg.SY + 1; y++) {
				for (int x = -1; x < bg.SX + 1; x++) {
					var rect = GetRect (x, y);

					var outside = false
						|| x < 0
						|| y < 0
						|| x >= bg.SX
						|| y >= bg.SY;
					char c = char.MaxValue;
					if (!outside) {
						c = bg.CellAt (x, y);
					}

					// closed?
					if (c == char.MaxValue) {
						draws.Add ((
							PRIO_CLOSED_CELL,
							draws.Count,
							g => g.DrawImage (_ImageCellClosed.Value, rect)
							));
						continue;
					}

					if (c != 0) {
						// covered by a piece, fill color

						var col = PieceColors [char.ToUpperInvariant (c)];
						draws.Add ((
							PRIO_USED_CELL,
							draws.Count,
							g => draw (col, g)
							));

						void draw (Color col, Graphics g)
						{
							using var brush = new SolidBrush (col);
							g.FillRectangle (brush, rect);
						}
					}

					// Draw cell edge for both empty and used cells
					draws.Add ((
						PRIO_EMPTY_CELL,
						draws.Count,
						g => g.DrawRectangle (pen, rect)
						));
				}
			}

			// Draw in order
			draws.Sort ();
			foreach (var tup in draws) {
				tup.a (g);
			}

			// Fill cells as requested
			if (printColor != null) {
				foreach (var pair in printColor) {
					int x = pair.cell % bg.SX;
					int y = pair.cell / bg.SX;
					var rect = GetRect (x, y);
					using var brush = new SolidBrush (pair.col);
					g.FillRectangle (brush, rect);
				}
			}

			// Draw text as requested
			if (printText != null) {
				using (var font = new Font ("Segoe UI", 12)) {
					foreach (var pair in printText) {
						int x = pair.cell % bg.SX;
						int y = pair.cell / bg.SX;
						var rect = GetRect (x, y);
						g.DrawString (pair.text, font, Brushes.Black, rect);
					}
				}
			}

			// Ensure no mis-scaling by the printer driver
			g.DrawRectangle (pen, 0, 0, sx, sy);

			// Draw board information
			var info = ""
				+ $"v2.2 {description}; {parameters.Name} export\n"
				+ $"{bg.ExtendedName}\n"
				+ $"{description2}";
			using (var font = new Font ("Courier New", 16)) {
				g.DrawString (info, font, Brushes.Black, 4, 4);
			}
		}

		return bmp;
	}
}
