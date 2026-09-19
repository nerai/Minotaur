using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Minotaur.Boards.Common;
using Minotaur.Boards.Dancing;
using Minotaur.Boards.Postgre;

namespace Minotaur
{
	public class TestSetGenerator
	{
		public static void CreateSpecifiedSet (
			string setName,
			string setDir,
			string setDesc,
			IReadOnlyList<string> hashes,
			bool indexStartsAt1 = false,
			Func<int, string> largeInfo = null)
		{
			Directory.CreateDirectory (setDir);

			var textRows = new string [hashes.Count];
			void copyToOutput (int index, string hash)
			{
				var ss = Solver.Solved (hash);
				var printIndex = index + (indexStartsAt1 ? 1 : 0);
				var info = $"{setDesc} @{printIndex:00}";
				var bmpPath = $"{setDir}/dina4 {printIndex:00}.png";
				var li = largeInfo == null ? "" : largeInfo (index);
				CreateBoardImage (bmpPath, info, ss, li);

				var diffT = ss.Steps;
				var diff0 = ss.StepsToFirst;
				var id = ss.Grid.InvariantName;
				textRows [index] = $"{id}\t{index:0}\t{diffT:0.0}\t{diff0:0.0}\t";
			}

			var text = $"Set {setName}\tIndex\tDifficulty-T\tDifficulty-0\t\n";
			//Parallel.For (0, hashes.Count, toIndex => {
			for (int toIndex = 0; toIndex < hashes.Count; ++toIndex) {
				copyToOutput (toIndex, hashes [toIndex]);
			}
			//});
			text += string.Join ("\n", textRows);

			File.WriteAllText ($"{setDir}/index_generated.tsv", text, Encoding.UTF8);
			Console.WriteLine ($"Created set {setDir}");
		}

		public static void CreateBoardImage (string bmpPath, string info, Solver ss, string largeInfo)
		{
			var bmp = BoardGridDrawer.AsImage (
				ss.Grid,
				ss.Description,
				BoardGridDrawer.Export_DinA4,
				description2: info);
			using (var g = Graphics.FromImage (bmp)) {
				g.SmoothingMode = SmoothingMode.AntiAlias;
				g.InterpolationMode = InterpolationMode.HighQualityBicubic;
				g.PixelOffsetMode = PixelOffsetMode.HighQuality;
				g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

				using (var font = new Font ("Courier New", 18)) {
					var sf = new StringFormat (StringFormatFlags.DirectionVertical);
					g.DrawString (largeInfo, font, Brushes.Black, 10, 100, sf);
				}
			}
			bmp.Save (bmpPath, ImageFormat.Png);
		}
	}
}
