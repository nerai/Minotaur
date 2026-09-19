

namespace Minotaur.Boards.Common;

public interface INodePool<TGrid, TNode>
	where TGrid : ISolutionGrid
	where TNode : TreeNode<TGrid>
{
	TNode GetOrCreate (TGrid grid, TreeAction<TGrid> commisioner);

	void ReturnNode (TNode grid);
}
