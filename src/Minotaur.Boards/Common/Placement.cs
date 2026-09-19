using System;

namespace Minotaur.Boards.Common;

public class Placement
{
	public readonly Piece P;
	public readonly byte X;
	public readonly byte Y;
	public readonly byte Rot;

	public Placement (Piece p, byte x, byte y, byte rot)
	{
		P = p ?? throw new ArgumentNullException (nameof (p));
		X = x;
		Y = y;
		Rot = rot;
	}
}
