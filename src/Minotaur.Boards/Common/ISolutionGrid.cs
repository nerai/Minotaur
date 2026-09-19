using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using static Minotaur.Boards.Common.BoardGrid;

namespace Minotaur.Boards.Common;

public interface ISolutionGrid : IHashable
{
	string TextualRepresentation (TextualRepresentationStyle style, List<int> highlight = null);

	bool IsClosedAt (int x, int y);

	(int SX, int SY) Size { get; }

	int UnfinishedCells { get; }
}
