using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Minotaur.Boards.Common;
using Minotaur.Boards.Drawing.Generic;
using Minotaur.Utils;

namespace Minotaur.Boards.Drawing;

public class WormyBoardDrawerCommon
{
	// Size parameters for cells in the image
	internal readonly int CellSize;
	internal readonly int BorderSize;

	// Size and offset, in pixels
	internal int _pSX;
	internal int _pSY;
	internal int _pX0;
	internal int _pY0;

	public readonly List<Color> _CellColor;

	// Some other properties that may differ for large and small drawing
	internal readonly Font FontText;
	internal readonly Font FontRect;

	public WormyBoardDrawerCommon (ISolutionGrid gridAtRoot, bool large)
	{
		if (large) {
			CellSize = 34;
			BorderSize = 2;
			FontText = ImgOutputShared._Font_Pre12;
			FontRect = ImgOutputShared._Font_Pre20;
		}
		else {
			CellSize = 20;
			BorderSize = 1;
			FontText = ImgOutputShared._Font_Pre8;
			FontRect = ImgOutputShared._Font_Pre12;
		}

		var grid = gridAtRoot;
		if (false) {
			// Almost half a cell of border space
			_pSX = CellSize * grid.Size.SX + CellSize;
			_pSY = CellSize * grid.Size.SY + CellSize;
		}
		else {
			// Just enough room to fit in a full border (cells extend outwards!)
			_pSX = CellSize * grid.Size.SX + BorderSize * 2;
			_pSY = CellSize * grid.Size.SY + BorderSize * 2;
		}
		_pX0 = (_pSX - grid.Size.SX * CellSize) / 2;
		_pY0 = (_pSY - grid.Size.SY * CellSize) / 2;

		/*
		 * Prepare colors for all cells
		 */
		_CellColor = new List<Color> ();
		var r = new Random ((int) Hashing.SDBM (gridAtRoot.GetUniqueHash ().AsSpan ()));
		for (int i = 0; i < gridAtRoot.Size.SX * gridAtRoot.Size.SY; i++) {
			/*
			var h = 1.0 * i / (grid.SX * grid.SY);
			var s = (i & 2) == 0 ? 0.65 : 0.75;
			var l = (i & 1) == 0 ? 0.50 : 0.70;
			var c = Util.hslToRgb (h, s, l);
			*/

			var cR = 0;
			var cG = 0;
			var cB = 0;
			do {
				cR = r.Next (0, 255);
				cG = r.Next (0, 255);
				cB = r.Next (0, 255);
			}
			while (cR + cG + cB < 3 * 255 * 0.65 || cR + cG + cB > 3 * 255 * 0.94);

			var c = Color.FromArgb (cR, cG, cB);
			_CellColor.Add (c);
		}
	}

	internal Rectangle GetRect (int x, int y, int spanX = 1, int spanY = 1)
	{
		var rect = new Rectangle (
			_pX0 + x * CellSize,
			_pY0 + y * CellSize,
			CellSize * spanX,
			CellSize * spanY);
		return rect;
	}

	public static (BlockingCollection<Action> queue, Thread thread) CreateBackgroundDrawingQueue (string purpose)
	{
		var qWriteImages = new BlockingCollection<Action> ();
		var tWriteImages = new Thread (() => {
			if (1111 == 1111) {
				Parallel.ForEach (
					qWriteImages.GetConsumingEnumerable (),
					a => a ());
			}
			else {
				foreach (var a in qWriteImages.GetConsumingEnumerable ()) {
					a ();
				}
			}
		}) {
			Name = $"Wormy background drawing queue of {purpose}",
			Priority = ThreadPriority.Lowest,
		};
		tWriteImages.Start ();

		return (qWriteImages, tWriteImages);
	}
}
