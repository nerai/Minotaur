using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;

namespace Minotaur.Boards.Drawing.Generic;

public class ImgOutputShared
{
	public static readonly Font _Font_Pre8 = new Font ("Consolas", 8f, FontStyle.Regular);
	public static readonly Font _Font_Pre9 = new Font ("Consolas", 9f, FontStyle.Regular);
	public static readonly Font _Font_Pre12 = new Font ("Consolas", 12f, FontStyle.Regular);
	public static readonly Font _Font_Pre14 = new Font ("Consolas", 14f, FontStyle.Regular);
	public static readonly Font _Font_Pre16 = new Font ("Consolas", 16f, FontStyle.Regular);
	public static readonly Font _Font_Pre20 = new Font ("Consolas", 20f, FontStyle.Regular);
	public static readonly Font _Font_Pre30 = new Font ("Consolas", 30f, FontStyle.Regular);
	//public static readonly Font _Font_Pre = new Font ("Lucida Sans Unicode", 8f, FontStyle.Regular);
}
