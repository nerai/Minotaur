using System;
using System.Linq;

namespace Minotaur.Boards.Common;

public enum ChildEnumerationStrategy
{
	/// <summary>
	/// Total sum of child nodes.
	/// Minimum total count of nodes.
	/// </summary>
	MinTotal,

	/// <summary>
	/// todo
	/// </summary>
	MinBranches,

	/// <summary>
	/// Minimum of the pair.
	/// Have at least one branch that is very short.
	/// </summary>
	MinMin,

	/// <summary>
	/// Sum of child nodes strongly weighted for wrong solutions.
	/// Keep wrong branches short.
	/// </summary>
	MinWrong,

	/// <summary>
	/// Minimum of the pair.
	/// Have at least one branch that is very short.
	/// </summary>
	Complete,
}
