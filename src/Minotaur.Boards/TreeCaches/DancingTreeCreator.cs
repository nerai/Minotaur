using System.Diagnostics;
using Minotaur.Boards.Common;
using Minotaur.Boards.Dancing;

namespace Minotaur.Boards.TreeCaches;

public class DancingTreeCreator
{
	public DancingTreeCreator ()
	{
	}

	public DancingSolutionNode CreateTree (string hash)
	{
		var ss = new Solver (hash);
		ss.Solve (
			createSolutionTree: true,
			exploreOnlyOneColumn: true,
			randomize: false);
		Debug.Assert (ss.TreeRoot.SolvabilityIsKnown);
		return ss.TreeRoot;
	}
}
