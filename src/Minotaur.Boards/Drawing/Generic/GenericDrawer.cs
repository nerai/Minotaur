using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Minotaur.Boards.Drawing.Generic;

public abstract class GenericDrawer
{
	protected readonly List<(int prio, int order, Action a)> _Draws = new ();

	protected void AddAction (int prio, Action a)
	{
		_Draws.Add ((prio, _Draws.Count, a));
	}

	public abstract void DrawAndFinish ();

	public abstract void SaveAs (string path);

	public abstract void DrawRect (
		int prio,
		Rectangle rect,
		Color color,
		int thickness);

	public abstract void FillRect (
		int prio,
		Rectangle rect,
		Color color,
		HatchStyle? style,
		string fade);

	public abstract void DrawString (
		int prio,
		string s,
		bool escapeString,
		string internalName,
		Font font,
		Color color,
		Rectangle rect,
		ContentAlignment align);

	public void FillRect (int prio, Color color, int x, int y, int sx, int sy)
	{
		FillRect (prio, new Rectangle (x, y, sx, sy), color, null, null);
	}
}
