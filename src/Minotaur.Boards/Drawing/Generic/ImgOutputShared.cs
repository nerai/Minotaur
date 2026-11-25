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
	public static readonly Font _Font_Pre8 = new("Consolas", 8f, FontStyle.Regular);
	public static readonly Font _Font_Pre9 = new("Consolas", 9f, FontStyle.Regular);
	public static readonly Font _Font_Pre12 = new("Consolas", 12f, FontStyle.Regular);
	public static readonly Font _Font_Pre14 = new("Consolas", 14f, FontStyle.Regular);
	public static readonly Font _Font_Pre16 = new("Consolas", 16f, FontStyle.Regular);
	public static readonly Font _Font_Pre20 = new("Consolas", 20f, FontStyle.Regular);
	public static readonly Font _Font_Pre30 = new("Consolas", 30f, FontStyle.Regular);
	//public static readonly Font _Font_Pre = new Font ("Lucida Sans Unicode", 8f, FontStyle.Regular);
}
