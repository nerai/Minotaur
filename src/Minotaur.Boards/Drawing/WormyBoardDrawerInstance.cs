using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using Minotaur.Boards.Common;
using Minotaur.Boards.Drawing.Generic;
using Minotaur.Boards.Strings;

namespace Minotaur.Boards.Drawing;

public class WormyBoardDrawerInstance
{
	// Constants for ordering drawing commands
	internal const int PRIO_PREAMBLE = 0;
	internal const int PRIO_BACKGROUND = 10;
	internal const int PRIO_CELL_EDGE = 20;
	internal const int PRIO_CELL_CONNECTION = 30;
	internal const int PRIO_CELL_INNER = 40;
	internal const int PRIO_CELL_HIGHLIGHT = 50;
	internal const int PRIO_CELL_FADE = 60;
	internal const int PRIO_FOREGROUND = 99;

	internal static readonly Color EmptyCellBackground = Color.FromArgb (240, 240, 240);

	private readonly WormyBoardDrawerCommon _Common;
	private readonly WormyNode _Node;
	private readonly WormyBoardGrid _Grid;
	private readonly Dictionary<Worm, HatchStyle> _Highlights;
	private readonly List<GenericDrawer> _Drawers = new ();

	public string Path;
	public readonly Dictionary<int, string> CellNames = new ();
	public readonly Dictionary<int, Color> CellColors = new ();
	public bool FadeEast = false;
	public bool FadeWest = false;
	public bool FadeNorth = false;
	public bool FadeSouth = false;
	public bool ConnectiblesWhite = true;
	public bool AutoName = false;

	private readonly IEnumerable<WormyAction> _Ancestors;

	/// <summary>
	/// Threadsafe.
	/// </summary>
	[SuppressMessage ("Interoperability", "CA1416:Validate platform compatibility")]
	public WormyBoardDrawerInstance (
		WormyBoardDrawerCommon wbdc,
		string path,
		WormyNode node,
		IEnumerable<TreeAction<WormyBoardGrid>> ancestors)
	{
		_Common = wbdc;
		Path = path;
		_Node = node;
		_Grid = node.Grid;
		_Ancestors = ancestors.Cast<WormyAction> ();

		_Highlights = new Dictionary<Worm, HatchStyle> ();
	}

	public bool DrawPng = true;
	public bool DrawSvg = true;
	public bool DrawTikz = true;

	/// <summary>
	/// This should be called only once per instance.
	/// </summary>
	public void Draw ()
	{
		if (DrawPng) {
			_Drawers.Add (new BitmapDrawer ((_Common._pSX, _Common._pSY)));
		}
		if (DrawSvg) {
			_Drawers.Add (new SvgDrawer ((_Common._pSX, _Common._pSY)));
		}
		if (DrawTikz) {
			_Drawers.Add (new TikzDrawer ((_Common._pSX, _Common._pSY)));
		}

		foreach (var dr in _Drawers) {
			dr.FillRect (
				PRIO_BACKGROUND,
				(1111 == 1111 ? Color.Transparent : Color.Gray),
				0, 0,
				_Common._pSX, _Common._pSY);
		}

		DrawAllCells ();

		// Draw board information
		if (1111 == 111) {
			var info = $"{Path}";
			foreach (var dr in _Drawers) {
				dr.DrawString (
					PRIO_FOREGROUND,
					info,
					true,
					"boardInformation",
					ImgOutputShared._Font_Pre8,
					Color.Black,
					Rectangle.Empty,
					ContentAlignment.TopLeft);
			}
		}

		foreach (var dr in _Drawers) {
			dr.DrawAndFinish ();
			dr.SaveAs (Path);
		}
	}

	/*
	void DrawText (Worm w, Color color, string text)
	{
		var min = w.Cells.Min ();
		var rect = _Common.GetRect (min % _Grid.SX, min / _Grid.SX);
		foreach (var dr in _Drawers) {
			dr.DrawString (
				PRIO_CELL_HIGHLIGHT,
				text,
				_Common.FontText,
				color,
				rect,
				ContentAlignment.MiddleCenter);
		}
	}
	*/

	public void CreateHighlights (WormyAction action)
	{
		if (action == null) {
			return;
		}

		switch (action) {
			case WormyAction_Bottleneck mincut:
				_Highlights.TryAdd (mincut.Cut, HatchStyle.ForwardDiagonal);
				foreach (var reach in mincut.Reachable) {
					_Highlights.TryAdd (reach, HatchStyle.SmallConfetti);
				}
				break;

			case WormyAction_Merge merge:
				_Highlights.TryAdd (merge.Pivot, HatchStyle.BackwardDiagonal);
				_Highlights.TryAdd (merge.Neighbour, HatchStyle.ForwardDiagonal);
				//DrawText (a.Pivot, Color.Black, "piv");
				//DrawText (a.Neighbour, Color.Black, "add");
				break;

			case WormyAction_Separation sep:
				throw new Exception ("use merge instead");

			case WormyAction_Choice choice:
				_Highlights.TryAdd (choice.Pivot, HatchStyle.SmallConfetti);
				_Highlights.TryAdd (choice.Neighbour, HatchStyle.SmallConfetti);
				foreach (var refused in choice.Refused) {
					_Highlights.TryAdd (refused, HatchStyle.ForwardDiagonal);
				}
				break;

			case WormyAction_Boardsplit bsplit:
				_Highlights.TryAdd (bsplit.Cut, HatchStyle.ForwardDiagonal);
				foreach (var reach in bsplit.Reachable) {
					_Highlights.TryAdd (reach, HatchStyle.SmallConfetti);
				}
				break;

			case WormyAction_Piece apc:
				foreach (var src in apc.SourceWorms) {
					_Highlights.TryAdd (src, HatchStyle.SmallConfetti);
				}
				break;

			default:
				throw new NotImplementedException ();
		}
	}

	/// <summary>
	/// Fixes width/height to be positive by adjusting the origin
	/// </summary>
	public static Rectangle Rect (int x, int y, int width, int height)
	{
		if (width < 0) {
			x += width;
			width = -width;
		}
		if (height < 0) {
			y += height;
			height = -height;
		}
		return new Rectangle (x, y, width, height);
	}

	private void DrawAllCells ()
	{
		for (int y = 0; y < _Grid.SY; y++) {
			for (int x = 0; x < _Grid.SX; x++) {
				DrawCellAt (y, x);
			}
		}

		void insertFade (
			int x0, int y0, // origin of the line: maximum fade
			int sx, int sy, // extend in this direction everywhere along the line
			int vx, int vy, // add to origin to get the end point of the line: minimum fade
			string direction)
		{
			/*
			 * Rects for
			 * a) total white cover
			 * b) partial white cover
			 */
			var rectOuter = Rect (
				x0,
				y0,
				sx + vx * 2 / 5,
				sy + vy * 2 / 5);
			var rectInner = Rect (
				x0 + vx * 2 / 5,
				y0 + vy * 2 / 5,
				sx + vx - vx * 2 / 5,
				sy + vy - vy * 2 / 5);

			foreach (var dr in _Drawers) {
				/*
				 * Pure white area
				 */
				dr.FillRect (
					PRIO_CELL_FADE,
					rectOuter,
					Color.White,
					null,
					null);
				/*
				 * Fade effect area
				 */
				dr.FillRect (
					PRIO_CELL_FADE,
					rectInner,
					Color.White,
					null,
					direction);
			}
		}

		var epsilon = 1;

		if (FadeWest) {
			var rect = _Common.GetRect (0, 0, spanY: _Grid.SY);
			int x0 = rect.X - _Common.BorderSize - epsilon;
			int y0 = rect.Y - _Common.BorderSize - epsilon;
			int sx = 0;
			int sy = rect.Height + 2 * _Common.BorderSize + 2 * epsilon;
			int vx = rect.Width + epsilon;
			int vy = 0;
			insertFade (x0, y0, sx, sy, vx, vy, "east");
		}

		if (FadeEast) {
			var rect = _Common.GetRect (_Grid.SX - 1, 0, spanY: _Grid.SY);
			int x0 = rect.Right + _Common.BorderSize + epsilon;
			int y0 = rect.Y - _Common.BorderSize - epsilon;
			int sx = 0;
			int sy = rect.Height + 2 * _Common.BorderSize + 2 * epsilon;
			int vx = -rect.Width - epsilon;
			int vy = 0;
			insertFade (x0, y0, sx, sy, vx, vy, "west");
		}

		if (FadeNorth) {
			var rect = _Common.GetRect (0, 0, spanX: _Grid.SX);
			int x0 = rect.X - _Common.BorderSize - epsilon;
			int y0 = rect.Y - _Common.BorderSize - epsilon;
			int sx = rect.Width + 2 * _Common.BorderSize + 2 * epsilon;
			int sy = 0;
			int vx = 0;
			int vy = rect.Height + epsilon;
			insertFade (x0, y0, sx, sy, vx, vy, "south");
		}

		if (FadeSouth) {
			var rect = _Common.GetRect (0, _Grid.SY - 1, spanX: _Grid.SX);
			int x0 = rect.X - _Common.BorderSize - epsilon;
			int y0 = rect.Bottom + _Common.BorderSize + epsilon;
			int sx = rect.Width + 2 * _Common.BorderSize + 2 * epsilon;
			int sy = 0;
			int vx = 0;
			int vy = -rect.Height - epsilon;
			insertFade (x0, y0, sx, sy, vx, vy, "north");
		}

		void DrawCellAt (int y, int x)
		{
			var rect = _Common.GetRect (x, y);
			var closed = false
				|| x < 0
				|| y < 0
				|| x >= _Grid.SX
				|| y >= _Grid.SY
				|| _Grid.IsClosedAt (x, y);
			if (closed) {
				return;
			}

			var i = x + y * _Grid.SX;
			var me = _Grid.WormInCell [i];
			var rig = x >= _Grid.SX - 1 ? null : _Grid.WormInCell [i + 1];
			var bot = y >= _Grid.SY - 1 ? null : _Grid.WormInCell [i + _Grid.SX];

			/*
			 * Color and hatch style of cell
			 */
			Color myColor;
			var given = CellColors
				.OrderBy (kvp => kvp.Key)
				.Cast<Nullable<KeyValuePair<int, Color>>> ()
				.FirstOrDefault (kvp => me.Cells.Contains (kvp.Value.Key));
			if (given != null) {
				myColor = given.Value.Value;
			}
			else if (me.Cells.Length <= 1) {
				myColor = EmptyCellBackground;
			}
			else {
				var origin = me;
				foreach (var ancestor in _Ancestors) {
					origin = ancestor.ParentOfWorm (origin);
				}

				/* old
				//Console.WriteLine ($"\nSearching for cell {x},{y}, worm {me}.");
				var origin = me;
				var uptree = _Node;
				while (uptree != null && uptree.Parents.Count () > 0) {
					var pair = uptree.Parents.First ();
					var a = (WormyAction) pair;
					origin = a.ParentOfWorm (origin);
					//Console.WriteLine ($"uptree {uptree.Name} ## a {a} ## origin {origin}");
					uptree = (WormyNode) pair.Parent.Parent;
				}
				if (origin.Cells.Length > 1) {
					// This may happen if and only if the root board contains a worm with 2+ cells - not usually the case
					// It also happens if the tree is a DAG and the above selected parent happens to be pruned. Annoying to fix, so just ignore this here.
					//Console.Error.WriteLine ($"WARNING: Worm origin contains multiple cells. {me}; {origin}");
					//Debugger.Break ();
				}
				*/

				var min = origin.Cells.Min ();
				myColor = _Common._CellColor [min];
			}

			HatchStyle? style = null;
			if (_Highlights.TryGetValue (me, out var dictstyle)) {
				style = dictstyle;
			}

			foreach (var dr in _Drawers) {
				var backgroundRect = rect;
				backgroundRect.X += _Common.BorderSize;
				backgroundRect.Y += _Common.BorderSize;
				backgroundRect.Width -= 2 * _Common.BorderSize;
				backgroundRect.Height -= 2 * _Common.BorderSize;
				dr.FillRect (PRIO_CELL_INNER, backgroundRect, myColor, style, null);
			}

			/*
			 * Text in cell
			 */
			string cellName;
			if (CellNames.Count > 0) {
				if (!CellNames.TryGetValue (i, out cellName)) {
					cellName = "";
				}
			}
			else if (AutoName) {
				cellName = $"{i,2}";
			}
			else {
				//Console.WriteLine ("WARNING: Behavior changed. Requires auto-name now.");
				cellName = "";
			}
			foreach (var dr in _Drawers) {
				dr.DrawString (
					PRIO_CELL_HIGHLIGHT,
					cellName,
					false,
					$"cell{i}",
					_Common.FontRect,
					Color.Black,
					rect,
					ContentAlignment.MiddleCenter);
			}

			if (1111 == 111) {
				// Old drawing code: Technically correct, but most PDF viewers display it wrongly
				foreach (var dr in _Drawers) {
					dr.DrawRect (
						PRIO_CELL_EDGE,
						rect,
						Color.Black,
						2 * _Common.BorderSize);
				}
			}
			else {
				/*
				 * Edge of cell = background (filled)
				 */
				var borderRect = rect;
				borderRect.X -= _Common.BorderSize;
				borderRect.Y -= _Common.BorderSize;
				borderRect.Width += 2 * _Common.BorderSize;
				borderRect.Height += 2 * _Common.BorderSize;
				foreach (var dr in _Drawers) {
					dr.FillRect (
						PRIO_CELL_EDGE,
						borderRect,
						Color.Black,
						null,
						null);
				}
			}

			/*
			 * Connectivity status
			 */
			void FillBorder (Worm towards, Rectangle fillRect)
			{
				if (me == towards) {
					foreach (var dr in _Drawers) {
						dr.FillRect (PRIO_CELL_CONNECTION, fillRect, myColor, style, null);
					}
				}
				else if (towards != null && me.HasNeighbour (towards)) {
					if (me.IsNotAllowedToConnectTo (towards)) {
						foreach (var dr in _Drawers) {
							dr.FillRect (PRIO_CELL_CONNECTION, fillRect, Color.Red, null, null);
						}
					}
					else {
						var col = ConnectiblesWhite ? EmptyCellBackground : Color.LimeGreen;
						foreach (var dr in _Drawers) {
							dr.FillRect (PRIO_CELL_CONNECTION, fillRect, col, null, null);
						}
					}
				}
			}

			/*
			 * Strategy: Overshoot into the cells.
			 * We intentionally do not only draw on the 'black' edge line, because that would cause visual artifacts (thin black lines).
			 */
			var overshootFactor = 2;
			var connectR = new Rectangle (
				rect.Right - overshootFactor * _Common.BorderSize,
				rect.Y + _Common.CellSize / 4,
				overshootFactor * 2 * _Common.BorderSize,
				_Common.CellSize / 2);
			var connectB = new Rectangle (
				rect.X + _Common.CellSize / 4,
				rect.Bottom - overshootFactor * _Common.BorderSize,
				_Common.CellSize / 2,
				overshootFactor * 2 * _Common.BorderSize);
			FillBorder (rig, connectR);
			FillBorder (bot, connectB);
		}
	}
}
