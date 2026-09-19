using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

namespace Minotaur.Boards.Drawing.Generic;

public class BitmapDrawer : GenericDrawer
{
	public static (int, int) Size_Screen1920 = (1920, 1040);
	public static (int, int) Size_Screen2560 = (2560, 1400);

	public static (Bitmap bmp, Graphics g) CreateEmptyImage ((int sx, int sy) size)
	{
		var (sx, sy) = size;
		var bmp = new Bitmap (sx, sy);
		var g = GraphicsFromBitmap (bmp);
		g.FillRectangle (Brushes.White, 0, 0, bmp.Width, bmp.Height);
		return (bmp, g);
	}

	public static (Bitmap bmp, Graphics g) CreateFromFile (string path)
	{
		Bitmap bmp;
		// Clone the bitmap to keep the file unlocked
		using (var raw = new Bitmap (path)) {
			bmp = new Bitmap (raw);
		}
		var g = GraphicsFromBitmap (bmp);
		return (bmp, g);
	}

	public static Graphics GraphicsFromBitmap (Image bmp)
	{
		var g = Graphics.FromImage (bmp);
		g.SmoothingMode = SmoothingMode.HighQuality;
		g.InterpolationMode = InterpolationMode.HighQualityBicubic;
		g.PixelOffsetMode = PixelOffsetMode.HighQuality;
		g.CompositingQuality = CompositingQuality.HighQuality;
		g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		return g;
	}

	private readonly Bitmap _Bmp;
	private readonly Graphics _G;

	public BitmapDrawer ((int _pSX, int _pSY) size)
	{
		(_Bmp, _G) = CreateEmptyImage (size);
	}

	public BitmapDrawer (string fromFile)
	{
		(_Bmp, _G) = CreateFromFile (fromFile);
	}

	public override void DrawAndFinish ()
	{
		_Draws.Sort ();
		foreach (var tup in _Draws) {
			tup.a ();
		}
		_G.Dispose ();
	}

	public override void SaveAs (string path)
	{
		if (!path.EndsWith (".png", StringComparison.InvariantCultureIgnoreCase)) {
			path = $"{path}.png";
		}
		if (File.Exists (path)) {
			File.Delete (path);
		}
		_Bmp.Save (path, ImageFormat.Png);
	}

	public override void DrawRect (
		int prio,
		Rectangle rect,
		Color color,
		int thickness)
	{
		Action a = () => {
			using var pen = new Pen (color, thickness);
			_G.DrawRectangle (pen, rect);
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
		Brush GetBrush ()
		{
			if (fade != null) {
				// TODO different directions
				return new SolidBrush (Color.FromArgb (127, color));
			}
			if (style.HasValue) {
				return new HatchBrush (style.Value, Color.Gray, color);
			}
			else {
				return new SolidBrush (color);
			}
		}

		// addEpsilon is ignored because we are pixel accurate

		Action a = () => {
			using var brush = GetBrush ();
			_G.FillRectangle (brush, rect);
		};
		AddAction (prio, a);
	}

	public override void DrawString (
		int prio,
		string s,
		bool escapeString,
		string internalName,
		Font font,
		Color brushColor,
		Rectangle rect,
		ContentAlignment align)
	{
		/*
		 * escapeString has no influence.
		 * 
		 * internalName has no influence.
		 */

		var fmt = new StringFormat (StringFormatFlags.LineLimit | StringFormatFlags.NoWrap, 1003) {
			Trimming = StringTrimming.None,
		};

		if (align == ContentAlignment.TopLeft) {
			fmt.Alignment = StringAlignment.Near;
		}
		else if (align == ContentAlignment.MiddleCenter) {
			fmt.Alignment = StringAlignment.Center;
		}
		else {
			throw new NotImplementedException ();
		}

		Action a = () => {
			using var brush = new SolidBrush (brushColor);
			_G.DrawString (
				s,
				font,
				brush,
				rect,
				fmt);
		};
		AddAction (prio, a);
	}
}
